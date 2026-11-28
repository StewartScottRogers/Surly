// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a pipeline step against a full disk: the uploader writes tonight's export over FTP, and the
// next step reads it over HTTP from the same server. /archive is out of space; elsewhere a file has
// 100,000 bytes of room.
// Run: dotnet run program.cs [-- <case>]
//   (none)      upload the export to /archive (the default)
//   incoming    upload it to /incoming, which has room
//   big         upload a 300,000-byte export to /incoming
//   last-night  upload nothing; the next step reads last night's export
//   http-put    upload the export to /archive with an HTTP PUT instead
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "archive";

// One server, one store: FTP for the uploader, HTTP for the next step. Its log is kept to read at the end.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0", "http://127.0.0.1:0" },
    AllowAnonymous = true,
    AllowUploads = true,
    MaxFileSize = 100_000,
    Faults = { SurlFault.OutOfSpace() with { Path = "/archive" } },
    Log = log,
    LogLevel = LogLevel.Verbose,
};
byte[] lastNight = "id,total\n1,1180\n2,940\n"u8.ToArray();
options.Files["archive/2026-11-19.csv"] = lastNight;
options.Files["incoming/README.txt"] = "drop exports here\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri ftp = server.BoundUrls[0], http = server.BoundUrls[1];

byte[] export = "id,total\n1,1200\n2,950\n"u8.ToArray();
if (which == "big")
{
    export = new byte[300_000];
    new Random(7).NextBytes(export);
}

string path = which switch
{
    "big" => "incoming/big.csv",
    "incoming" => "incoming/2026-11-20.csv",
    "last-night" => "archive/2026-11-19.csv",
    _ => "archive/2026-11-20.csv",
};
await using CurlClient client = CurlClient.Create();

// The uploader: a PUT with a body on an ftp:// URL is STOR (on http://, an HTTP PUT). A failure makes the
// test exit 1: its report.
int exitCode = 0;
if (which != "last-night")
{
    try
    {
        Uri listener = which == "http-put" ? http : ftp;
        CurlResponse stored = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{listener}{path}") { Body = export }, CancellationToken.None);
        await stored.Body.DisposeAsync();
        Console.WriteLine($"upload /{path}: {(which == "http-put" ? stored.StatusCode.ToString() : "stored")}");
        exitCode = stored.StatusCode >= 400 ? 1 : 0;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"upload /{path}: {failure.Code} ({failure.Message})");
        exitCode = 1;
    }
}

// The next step: read the same path over HTTP.
CurlResponse got = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{http}{path}"), CancellationToken.None);
var body = new MemoryStream();
await using (Stream stream = got.Body)
{
    await stream.CopyToAsync(body);
}

byte[] expected = which == "last-night" ? lastNight : export;
Console.WriteLine($"next step: {got.StatusCode}, {(got.StatusCode == 200 && body.ToArray().AsSpan().SequenceEqual(expected) ? "the export byte for byte" : $"{body.Length} bytes")}");

// The test's check: nothing half-written in the server's store, and what the server logged.
try
{
    SurlFileContents kept = await server.Files.ReadAsync("/" + path);
    Console.WriteLine($"store: {kept.Bytes.Length} bytes at /{path}");
}
catch (SurlNotFoundException)
{
    Console.WriteLine($"store: nothing at /{path}");
}

Console.WriteLine($"log: {log.ToString().Split('\n').Count(line => line.Contains("the partial upload was deleted"))} partial uploads deleted");
return exitCode;
