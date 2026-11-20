// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Starts a Curl.Api client before a Surl.Api server and retries until the server is listening,
// a bounded number of times. Run: dotnet run wait-for-server.cs -- [tries] [start]
//   tries   how many attempts the client may make in all (10 if not given)
//   start   when the server starts: seconds after the first refused attempt (2 if not given),
//           0 for before the client, or never
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Net;
using System.Net.Sockets;
using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Surl.Api;

int tries = args.Length > 0 ? int.Parse(args[0]) : 10;
string start = args.Length > 1 ? args[1] : "2";

// A port both sides agree on in advance, as a test fixture would: ask the system for a free one.
var probe = new TcpListener(IPAddress.Loopback, 0);
probe.Start();
int port = ((IPEndPoint)probe.LocalEndpoint).Port;
probe.Stop();
string url = $"http://127.0.0.1:{port}/hello.txt";

// The server, on its own schedule: before the client, some seconds after the client's first
// attempt is refused, or never. Counting from the refusal, not from the program's start, keeps a
// slow first request (the curl engine starting on a busy machine) from finding the server up.
var options = new SurlServerOptions { ListenUrls = { $"http://127.0.0.1:{port}" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
Task<SurlServer>? starting = start == "0" ? Task.FromResult(await SurlServer.StartAsync(options)) : null;
void StartLater()
{
    if (starting is null && start != "never")
    {
        starting = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(int.Parse(start)));
            return await SurlServer.StartAsync(options);
        });
    }
}

// The client: up to `tries` attempts, one second apart, retrying only a refused connection.
await using CurlClient client = CurlClient.Create();
CurlCode? failed = null;
for (int attempt = 1; attempt <= tries; attempt++)
{
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
        await using Stream body = response.Body;
        Console.Write($"attempt {attempt}: {response.StatusCode} {response.ReasonPhrase}, {await new StreamReader(body).ReadToEndAsync()}");
        failed = null;
        break;
    }
    catch (CurlException refused) when (refused.Code == CurlCode.CouldntConnect)
    {
        // Nothing listening yet: curl's exit code 7. Any other failure is not this program's to retry.
        Console.WriteLine($"attempt {attempt}: CurlException {(int)refused.Code} ({refused.Code})");
        failed = refused.Code;
        StartLater();
        if (attempt < tries)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}

if (failed is not null)
{
    Console.WriteLine($"gave up after {tries} attempts");
}

// Stop the server, if it started, and say how it stopped.
if (starting is not null)
{
    await using SurlServer server = await starting;
    Console.WriteLine($"server stopped: {await server.StopAsync()}");
}

// 0 once the file arrived; 7, curl's code for a refused connection, otherwise.
return failed is null ? 0 : (int)failed.Value;
