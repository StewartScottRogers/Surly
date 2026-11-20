// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A client uploads a file to an in-process Surl.Api server while the test waits for the
// store change, with no sleep and no polling.
// Run: dotnet run wait-for-upload.cs [-- late|anonymous|basic|off]
//   (no argument)  start the upload, wait for it, then read what landed
//   late           let the upload finish before the wait starts
//   anonymous      upload with no login
//   basic          upload with a Basic login over plain http://
//   off            leave RecordExchanges off
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "wait";

// The server: uploads on, one account, and the event record on.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    AllowUploads = true,
    RecordExchanges = mode != "off",
};
options.Accounts.Add(new SurlAccount("alice", "secret"));
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The code under test: a client that uploads a report. It is started, not awaited.
Task<int> upload = Upload();
if (mode == "late")
{
    Console.WriteLine($"PUT finished before the wait: {await upload}");
}

// The test: wait for the store change that names /report.csv.
var query = new SurlEventQuery { Store = StoreKind.Content, Name = "/report.csv" };
// A wait returns as soon as the event comes, so the timeout only bounds a failure: long enough
// for a loaded machine where one is expected, one second where none can come.
TimeSpan timeout = TimeSpan.FromSeconds(mode is "anonymous" or "basic" ? 1 : 30);
try
{
    var change = (StoreChangedEvent)await server.WaitForEventAsync(query, timeout);
    Console.WriteLine($"event {change.KindName}: {change.Change} {change.Name}, {change.ByteCount} bytes, exchange {change.ExchangeId}");
}
catch (TimeoutException failure)
{
    Console.WriteLine($"PUT answered {await upload}");
    Console.WriteLine(failure.Message);
    return 1;
}
catch (InvalidOperationException)
{
    Console.WriteLine($"PUT answered {await upload}");
    Console.WriteLine("RecordExchanges is off: the wait throws InvalidOperationException");
    return 1;
}

Console.WriteLine($"PUT answered {await upload}");

// What landed, read from the store without a client.
SurlFileContents file = await server.Files.ReadAsync("/report.csv");
Console.Write($"{file.ContentType}, {file.ByteCount} bytes:\n{Encoding.UTF8.GetString(file.Bytes.Span)}");
return 0;

async Task<int> Upload()
{
    CurlRequest put = new(HttpMethod.Put, $"{server.BoundUrl}report.csv") { Body = "region,total\nnorth,42\n"u8.ToArray() };
    if (mode != "anonymous")
    {
        put = put with
        {
            Authentication = new CurlAuthenticationOptions
            {
                UserName = "alice",
                Password = "secret",
                HttpAuthentication = mode == "basic" ? HttpAuthenticationMethods.Basic : HttpAuthenticationMethods.Digest,
            },
        };
    }

    CurlResponse response = await client.SendAsync(put, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode;
}
