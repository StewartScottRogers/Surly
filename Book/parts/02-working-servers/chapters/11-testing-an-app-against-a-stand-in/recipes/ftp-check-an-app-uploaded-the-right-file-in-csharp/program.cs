// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Runs an app that uploads a nightly export over FTP against an in-process Surl.Api server and checks, with the
// server's own Files.ReadAsync, that what arrived is byte for byte the expected file. The app is the Export method.
// Run: dotnet run program.cs -- <case>
//   matches        the app uploads the right export, the check passes
//   over-http      the same file, fetched over HTTP from the same server
//   broken-app     the app uploads the export without its last row, and still finishes
//   mismatch       the same app, then the check: it fails
//   missing        the check of a file the app never uploaded
//   uploads-off    a server that takes no uploads: the app
//   login-needed   a server with no anonymous login: the app
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;

string which = args[0];
byte[] expected = "id,region,total\n1,north,42\n2,south,17\n"u8.ToArray();

// The stand-in: one store, written over FTP and served over HTTP, in memory. Each listen URL needs a port of its own.
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0", "http://127.0.0.1:0" },
    AllowAnonymous = which != "login-needed",
    AllowUploads = which != "uploads-off",
};
await using SurlServer server = await SurlServer.StartAsync(options);
string ftp = server.BoundUrls[0].ToString();
string http = server.BoundUrls[1].ToString();

try
{
    switch (which)
    {
        case "matches":
            await Export(broken: false);
            return await Check("/exports/nightly.csv");
        case "over-http":
            await Export(broken: false);
            await using (CurlClient client = CurlClient.Create())
            {
                CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, http + "exports/nightly.csv"), CancellationToken.None);
                await using Stream body = response.Body;
                Console.Write($"{response.StatusCode}\n{await new StreamReader(body).ReadToEndAsync()}");
            }

            return 0;
        case "broken-app":
            await Export(broken: true);
            Console.WriteLine("app finished");
            return 0;
        case "mismatch":
            await Export(broken: true);
            return await Check("/exports/nightly.csv");
        case "missing":
            await Export(broken: false);
            return await Check("/exports/nightly-2.csv");
        default:
            await Export(broken: false);
            Console.WriteLine("app finished");
            return 0;
    }
}
catch (CurlException failure)
{
    Console.WriteLine($"refused: curl code {(int)failure.Code} {failure.Code}");
    return (int)failure.Code;
}

// The app: writes tonight's export and uploads it to exports/nightly.csv, making the folder if need be, as
// curl --ftp-create-dirs does. Given broken, it leaves out the last row, as a bug would.
async Task Export(bool broken)
{
    string export = broken ? "id,region,total\n1,north,42\n" : "id,region,total\n1,north,42\n2,south,17\n";
    await using CurlClient client = CurlClient.Create();
    var put = new CurlRequest(HttpMethod.Put, ftp + "exports/nightly.csv")
    {
        Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(export)),
        Ftp = new CurlFtpOptions { CreateMissingDirectories = FtpCreateMissingDirectories.Create },
    };
    await client.SendAsync(put, CancellationToken.None);
}

// The check: reads the file from the server's store, with no client and no login, and compares it byte for byte.
async Task<int> Check(string path)
{
    SurlFileContents arrived;
    try
    {
        arrived = await server.Files.ReadAsync(path);
    }
    catch (SurlNotFoundException)
    {
        Console.WriteLine($"nothing arrived at {path}");
        return 1;
    }

    if (arrived.Bytes.Span.SequenceEqual(expected))
    {
        Console.WriteLine("matches expected");
        return 0;
    }

    Console.WriteLine($"differs from expected: expected {expected.Length} bytes, arrived {arrived.ByteCount}");
    return 1;
}
