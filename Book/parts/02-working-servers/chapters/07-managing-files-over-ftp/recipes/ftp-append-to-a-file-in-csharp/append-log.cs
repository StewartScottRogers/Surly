// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A logger appends lines to a file on an in-process FTP server with Curl.Api, and the test reads the
// whole file back from the server's store.
// Run: dotnet run append-log.cs [-- replace|edges|uploads-off|too-big|no-login]
//   (no argument) two appends, the file after each, and the commands the server received
//   replace       the same upload without Append, which replaces the file
//   edges         an append to a file that is not there, and to one with no final newline
//   uploads-off   a server that accepts no uploads
//   too-big       a server whose size limit the appended file would pass
//   no-login      a server that accepts no anonymous login
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "append";

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = which != "no-login",
    AllowUploads = which != "uploads-off",
    MaxFileSize = which == "too-big" ? 15 : null,
    RecordExchanges = true,
};
options.Files["log.txt"] = "first line\n"u8.ToArray();
options.Files["replace.txt"] = "old contents\n"u8.ToArray();
options.Files["no-newline.txt"] = "no newline"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

// One client for the whole run, so every request is on one control connection and one exchange.
await using CurlClient client = CurlClient.Create();

try
{
    switch (which)
    {
        case "append":
            await Send("log.txt", "second line\n", append: true);
            Console.WriteLine($"log.txt: {await Read("log.txt")}");
            await Send("log.txt", "third line\n", append: true);
            Console.WriteLine($"log.txt: {await Read("log.txt")}");
            Console.WriteLine($"commands: {string.Join(", ", await Commands(2))}");
            break;
        case "replace":
            await Send("replace.txt", "third line\n", append: false);
            Console.WriteLine($"replace.txt: {await Read("replace.txt")}");
            Console.WriteLine($"commands: {string.Join(", ", await Commands(1))}");
            break;
        case "edges":
            await Send("fresh.txt", "second line\n", append: true);
            Console.WriteLine($"fresh.txt: {await Read("fresh.txt")}");
            await Send("no-newline.txt", "second line\n", append: true);
            Console.WriteLine($"no-newline.txt: {await Read("no-newline.txt")}");
            break;
        default:
            await Send("log.txt", "second line\n", append: true);
            break;
    }
}
catch (CurlException failure)
{
    Console.WriteLine($"{which}: CurlException {(int)failure.Code} {failure.Code}");
    Console.WriteLine($"log.txt: {await Read("log.txt")}");
}

return 0;

// One upload: STOR, or APPE when Append is set, which is curl --append.
async Task Send(string name, string text, bool append)
{
    var request = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{name}")
    {
        Body = Encoding.UTF8.GetBytes(text),
        Ftp = new CurlFtpOptions { Append = append },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
}

// The file as the server holds it, with its line ends written out.
async Task<string> Read(string name)
{
    SurlFileContents file = await server.Files.ReadAsync("/" + name);
    return Encoding.UTF8.GetString(file.Bytes.Span).Replace("\n", "\\n");
}

// The upload commands the server received, with the names they carried. The record can trail the client
// by a moment, so look again until it holds one per upload, for up to 10 s.
async Task<string[]> Commands(int uploads)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    while (Recorded().Length < uploads && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
    }

    return Recorded();
}

string[] Recorded() =>
    Encoding.ASCII.GetString(server.Exchanges[0].Received).Split("\r\n")
        .Where(line => line.StartsWith("APPE ") || line.StartsWith("STOR ")).ToArray();
