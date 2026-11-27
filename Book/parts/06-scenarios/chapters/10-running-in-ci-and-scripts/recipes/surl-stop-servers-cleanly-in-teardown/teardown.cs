// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test with two in-process servers, HTTP and FTP, that throws; its teardown stops every
// server in a finally, so the next run can bind the very same ports.
// Run: dotnet run teardown.cs [-- teardown|no-teardown|clients-last]
//   teardown      clients disposed, then every server stopped (the default)
//   no-teardown   the servers are left running, as a test without a finally leaves them
//   clients-last  the servers are stopped while a client still holds a connection
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Diagnostics;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "teardown";

// Set-up: each server on a free port.
var servers = new List<SurlServer>();
CurlClient client = CurlClient.Create();
try
{
    servers.Add(await Start("http://127.0.0.1:0"));
    servers.Add(await Start("ftp://127.0.0.1:0"));

    // The test: a fetch over each. The FTP one throws, since no account lets it log in.
    foreach (SurlServer server in servers)
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
        await response.Body.DisposeAsync();
        Console.WriteLine($"{server.BoundUrl.Scheme}: {response.StatusCode}");
    }
}
catch (CurlException failure)
{
    Console.WriteLine($"the test threw: CurlException {failure.Code} ({(int)failure.Code})");
}
finally
{
    // Teardown: clients first, so no kept-alive connection holds a server up; then every
    // server, each stop in its own try so one failure cannot leave the rest running.
    if (mode == "teardown")
    {
        await client.DisposeAsync();
    }

    if (mode != "no-teardown")
    {
        var clock = Stopwatch.StartNew();
        var codes = new List<SurlExitCode>();
        foreach (SurlServer server in servers)
        {
            try
            {
                codes.Add(await server.StopAsync());
            }
            catch (Exception stopFailure)
            {
                Console.WriteLine($"stop failed: {stopFailure.Message}");
            }
        }

        Console.WriteLine($"teardown: {string.Join(", ", codes)}, {(clock.Elapsed < TimeSpan.FromSeconds(3) ? "under" : "over")} 3 s");
    }
}

// The next run asks for the very same ports.
try
{
    foreach (SurlServer server in servers)
    {
        await using SurlServer next = await Start(server.BoundUrl.ToString());
        Console.WriteLine($"next run bound {next.BoundUrl.Scheme} on the same port");
    }

    return 0;
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"next run refused: {refused.ExitCode} ({(int)refused.ExitCode})");
    return (int)refused.ExitCode;
}
finally
{
    // Clean up what this program left behind, so it leaves nothing running.
    await client.DisposeAsync();
    foreach (SurlServer server in servers)
    {
        await server.DisposeAsync();
    }
}

// A server on one listen URL, serving hello.txt from memory.
static async Task<SurlServer> Start(string listenUrl)
{
    var options = new SurlServerOptions { ListenUrls = { listenUrl } };
    options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
    return await SurlServer.StartAsync(options);
}
