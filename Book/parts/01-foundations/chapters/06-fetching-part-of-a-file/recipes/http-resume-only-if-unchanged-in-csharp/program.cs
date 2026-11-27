// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A download in two halves, with the served file replaced in between; the resume sends If-Range,
// so a changed file comes back whole instead of being spliced onto the first half.
// Run: dotnet run program.cs [-- <case>]
//   (none)       the file is replaced between the halves; the resume sends the ETag
//   unchanged    the file is left alone; the resume sends the ETag
//   no-if-range  the file is replaced; the resume sends Range alone
//   date         the server's clock stands still, so the replacement keeps the same Last-Modified;
//                the resume is tried with the date and with the ETag
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "changed";
byte[] v1 = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 10).Select(i => $"version 1 line {i:00}\n")));
byte[] v2 = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 10).Select(i => $"version 2 line {i:00}\n")));

// The server, in process, with version 1 of the file. RecordExchanges keeps what each request carried.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
if (which == "date")
{
    options.TimeProvider = new StoppedClock(DateTimeOffset.UtcNow);
}
options.Files["records.txt"] = v1;
await using SurlServer server = await SurlServer.StartAsync(options);
string url = $"{server.BoundUrl}records.txt";
await using CurlClient client = CurlClient.Create();

// The first half: bytes 0 to 99, standing in for a download that was interrupted. Keep its validators.
(int status, byte[] got, string? etag, string? lastModified) = await Get(url, ("Range", "bytes=0-99"));
Console.WriteLine($"first half: {status}, {got.Length} bytes, ETag {(etag is null ? "missing" : "kept")}");
byte[] local = got;

// The publisher replaces the file between the halves.
if (which != "unchanged")
{
    await server.Files.PutAsync("/records.txt", which == "date" ? [.. v2, .. "version 2 line 10\n"u8] : v2);
}

if (which == "date")
{
    (int byDate, _, _, string? newDate) = await Get(url, ("Range", $"bytes={local.Length}-"), ("If-Range", lastModified!));
    (int byTag, _, string? newTag, _) = await Get(url, ("Range", $"bytes={local.Length}-"), ("If-Range", etag!));
    Console.WriteLine($"Last-Modified unchanged: {newDate == lastModified}, ETag unchanged: {newTag == etag}");
    Console.WriteLine($"If-Range with the date: {byDate}");
    Console.WriteLine($"If-Range with the ETag: {byTag}");
    return 0;
}

// The second half: the rest, but only if the file is still the version the first half came from.
(string, string)[] resume = which == "no-if-range"
    ? [("Range", $"bytes={local.Length}-")]
    : [("Range", $"bytes={local.Length}-"), ("If-Range", etag!)];
(int second, byte[] rest, _, _) = await Get(url, resume);
local = second switch
{
    206 => [.. local, .. rest],   // the same version: append
    200 => rest,                  // a different version: the whole of it replaces the first half
    _ => throw new InvalidOperationException($"unexpected {second}"),
};
Console.WriteLine($"second half: {second}, {rest.Length} bytes");
Console.WriteLine($"result: {(local.SequenceEqual(v1) ? "version 1" : local.SequenceEqual(v2) ? "version 2" : "neither version")}, {local.Length} bytes");

// What the server received for the second half.
string head = Encoding.ASCII.GetString(server.Exchanges[^1].Received.ToArray());
string lastRequest = head[head.LastIndexOf("GET ", StringComparison.Ordinal)..];
foreach (string line in lastRequest.Split("\r\n").Where(l => l.StartsWith("Range:") || l.StartsWith("If-Range:")))
{
    Console.WriteLine($"server saw: {(line.StartsWith("If-Range:") ? "If-Range: <the ETag of the first half>" : line)}");
}
return 0;

async Task<(int Status, byte[] Body, string? ETag, string? LastModified)> Get(string target, params (string Name, string Value)[] headers)
{
    var request = new CurlRequest(HttpMethod.Get, target) { Headers = [.. headers.Select(h => new KeyValuePair<string, string>(h.Name, h.Value))] };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    using var body = new MemoryStream();
    await using (Stream stream = response.Body)
    {
        await stream.CopyToAsync(body);
    }
    string? Header(string name) => response.Headers.Where(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).FirstOrDefault();
    return (response.StatusCode, body.ToArray(), Header("ETag"), Header("Last-Modified"));
}

// A clock that does not move, so a rewrite gets the same Last-Modified as the file it replaces.
sealed class StoppedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
