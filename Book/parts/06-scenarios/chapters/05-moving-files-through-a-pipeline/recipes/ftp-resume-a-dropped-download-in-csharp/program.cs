// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a resuming download over FTP: the server drops the connection at the first RETR, and the
// client asks again from the end of what it already has, until the file is whole.
// Run: dotnet run program.cs [-- <case>]
//   (none)    a partial copy of 65,536 bytes is resumed (the default)
//   longer    the local copy is longer than the server's file
//   checked   the same resume without IgnoreContentLength
//   whole     the local copy is already the whole file
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "resume";

// The image: 200,000 bytes that repeat from run to run.
byte[] image = new byte[200_000];
new Random(7).NextBytes(image);

// The server: anonymous FTP, the image in memory, and one rule that drops the connection, unanswered,
// at the first RETR of the image. Its log is kept to count the restarts it answered.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
    Faults = { SurlFault.DropConnection() with { Stage = FaultStage.Command, Command = "RETR", Path = "/image.iso", Times = 1 } },
};
options.Files["image.iso"] = image;
await using SurlServer server = await SurlServer.StartAsync(options);

// What a download cut off at 65,536 bytes leaves on disk; for "longer", another file of 300,000 bytes;
// for "whole", the whole image.
const string Local = "image.iso";
File.WriteAllBytes(Local, which switch { "longer" => new byte[300_000], "whole" => image, _ => image[..65_536] });

await using CurlClient client = CurlClient.Create();
for (int attempt = 1; attempt <= 3; attempt++)
{
    long have = new FileInfo(Local).Length;
    try
    {
        // REST asks the server to start after the bytes we have; it is sent after TYPE, just before RETR.
        // IgnoreContentLength stops the client checking the bytes against SIZE, which is the whole file's.
        var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}image.iso")
        {
            Ftp = new CurlFtpOptions { PreQuote = [$"REST {have}"] },
            Http = new CurlHttpOptions { IgnoreContentLength = which != "checked" },
        };
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using (Stream body = response.Body)
        await using (FileStream file = new(Local, FileMode.Append))
        {
            await body.CopyToAsync(file);
        }

        Console.WriteLine($"try {attempt}: from {have}: {new FileInfo(Local).Length - have} bytes");
        break;
    }
    catch (CurlException failure)
    {
        // A dropped connection is worth another try from the new end of the file; anything else is not.
        Console.WriteLine($"try {attempt}: from {have}: {failure.Code}, copy still {new FileInfo(Local).Length} bytes");
        if (failure.Code != CurlCode.RecvError)
        {
            break;
        }
    }
}

// The test's check: the local file against the server's, and the restarts the server answered.
byte[] got = File.ReadAllBytes(Local);
bool same = got.AsSpan().SequenceEqual(image);
Console.WriteLine($"{Local}: {got.Length} bytes, {(same ? "identical to the server's" : "DIFFERENT from the server's")}");
Console.WriteLine($"server answered: {log.ToString().Split('\n').Count(line => line.Contains("> 350 Restarting at"))} restarts");
Console.WriteLine($"server dropped: {log.ToString().Split('\n').Count(line => line.Contains("RETR dropped the connection, unanswered, by fault rule f1"))}");
return same ? 0 : 1;
