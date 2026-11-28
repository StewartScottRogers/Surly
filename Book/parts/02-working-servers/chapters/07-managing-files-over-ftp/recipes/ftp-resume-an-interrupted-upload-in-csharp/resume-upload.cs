// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Resumes an interrupted FTP upload against an in-process server: asks for the size of the file the server
// holds, sends the rest of the local file as an append, and compares what the server then holds.
// Run: dotnet run resume-upload.cs -- MODE
//   resume       a server holding the first 65,536 bytes of 200,000, resumed
//   commands     the SIZE, APPE and STOR the server received for that resume
//   fresh        a server holding nothing
//   complete     a server holding the whole file
//   restart      the whole file sent again with no append, over the partial one
//   offset       an offset of 100000 given, so no size is asked for
//   zeros        a partial file of zeros the right length
//   longer       a server file longer than the local one
//   uploads-off  a server that accepts no uploads
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "resume";

// The local file: 200,000 bytes, the same on every run.
byte[] local = new byte[200_000];
new Random(2026).NextBytes(local);

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = true,
    AllowUploads = mode != "uploads-off",
    RecordExchanges = true,
};
await using SurlServer server = await SurlServer.StartAsync(options);

// What the interrupted upload left on the server, the first 65,536 bytes unless the mode says otherwise.
byte[] partial = mode switch
{
    "fresh" => [],
    "complete" => local,
    "zeros" => new byte[65_536],
    "longer" => new byte[300_000],
    _ => local[..65_536],
};
if (partial.Length > 0)
{
    await server.Files.PutAsync("/update.iso", partial);
}

try
{
    // An offset given outright, as curl -C 100000 does; otherwise the offset is the size the server reports.
    long sent = await Resume("update.iso", mode == "offset" ? 100_000 : null);
    byte[] held = (await server.Files.ReadAsync("/update.iso")).Bytes.ToArray();
    Console.WriteLine(mode switch
    {
        "commands" or "offset" => $"{mode}: sent {sent}; {string.Join(", ", await Commands())}",
        "zeros" => $"zeros: sent {sent}, same as local: {held.AsSpan().SequenceEqual(local)}",
        "longer" => $"longer: sent {sent}, server holds {held.Length}",
        "complete" => $"complete: sent {sent}",
        _ => $"{mode}: sent {sent}, same as local: {held.AsSpan().SequenceEqual(local)}",
    });
}
catch (CurlException failure)
{
    byte[] held = (await server.Files.ReadAsync("/update.iso")).Bytes.ToArray();
    Console.WriteLine($"{mode}: CurlException {(int)failure.Code} {failure.Code}, server holds {held.Length}");
}

return 0;

// The size of a remote file, as curl -C - asks for it: a HEAD request, which sends SIZE and no more, whose answer
// arrives as Content-Length. A file that is not there is 0.
async Task<long> RemoteSize(string name)
{
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Head, $"{server.BoundUrl}{name}"), CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        return long.Parse(response.Headers.Single(h => h.Key == "Content-Length").Value);
    }
    catch (CurlException missing) when (missing.Code == CurlCode.RemoteFileNotFound)
    {
        return 0;
    }
}

// The rest of the local file after the offset, sent as an append, or all of it as an ordinary upload when
// the offset is 0. Returns how many bytes went. Nothing is sent when nothing is left.
async Task<long> Resume(string name, long? given)
{
    long offset = given ?? (mode == "restart" ? 0 : await RemoteSize(name));
    byte[] rest = offset >= local.Length ? [] : local[(int)offset..];
    if (rest.Length == 0)
    {
        return 0;
    }

    var request = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{name}")
    {
        Body = rest,
        // Append is curl -T --append: APPE, where a plain upload is STOR.
        Ftp = new CurlFtpOptions { Append = mode != "restart" && offset > 0 },
    };
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return rest.Length;
}

// The commands that ask for a size or store bytes, in order, with the names they carried. The record can trail
// the client by a moment, so look again until it holds the upload's APPE or STOR, for up to 10 s.
async Task<string[]> Commands()
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    string[] lines = Recorded();
    while (!lines.Any(l => l.StartsWith("APPE ") || l.StartsWith("STOR ")) && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
        lines = Recorded();
    }

    return lines;
}

string[] Recorded() =>
    server.Exchanges.SelectMany(e => Encoding.ASCII.GetString(e.Received).Split("\r\n"))
        .Where(line => line.StartsWith("SIZE ") || line.StartsWith("APPE ") || line.StartsWith("STOR ") || line.StartsWith("REST ")).ToArray();
