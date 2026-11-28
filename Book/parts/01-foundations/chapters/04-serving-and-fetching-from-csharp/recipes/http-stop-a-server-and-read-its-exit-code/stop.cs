// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Stops an in-process Surl.Api server explicitly and fails the run unless it stopped Ok.
// Run: dotnet run stop.cs [-- case]
//   stop          serve, fetch, stop, and fetch again (the default)
//   twice         stop the same server twice
//   client-open   stop while a client still holds its kept-alive connection
//   folder        serve site/ from disk, stop, and start a new server on the same folder
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Diagnostics;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "stop";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
if (which == "folder")
{
    options.DataDirectory = "site";
}
else
{
    options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
}

SurlServer server = await SurlServer.StartAsync(options);
string url = $"{server.BoundUrl}hello.txt";

// Each fetch has a client of its own, disposed with its connection once the fetch is done.
// client-open keeps one client, and its connection, past the stop.
CurlClient? kept = which == "client-open" ? CurlClient.Create() : null;
await Fetch("before stop", kept);

// Stop explicitly: StopAsync returns the exit code a surl process would have exited with.
var clock = Stopwatch.StartNew();
SurlExitCode code = await server.StopAsync();
Console.WriteLine($"StopAsync: {code} ({(int)code}), {(clock.Elapsed.TotalSeconds < 3 ? "under" : "over")} 3 s");

switch (which)
{
    case "twice":
        Console.WriteLine($"StopAsync again: {await server.StopAsync()}");
        break;

    case "folder":
        // The folder's lock went with the server, so a new server may take it.
        await using (SurlServer next = await SurlServer.StartAsync(options))
        {
            url = $"{next.BoundUrl}hello.txt";
            await Fetch("new server", null);
        }

        break;

    case "stop":
        await Fetch("after stop", null);
        break;
}

if (kept is not null)
{
    await kept.DisposeAsync();
}

// Fail the run unless serving went right: 0 for Ok, surl's own code otherwise.
return code == SurlExitCode.Ok ? 0 : (int)code;

async Task Fetch(string when, CurlClient? shared)
{
    CurlClient client = shared ?? CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
        await using Stream body = response.Body;
        Console.WriteLine($"{when}: {response.StatusCode} {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{when}: CurlException {(int)failure.Code} ({failure.Code})");
    }
    finally
    {
        if (shared is null)
        {
            await client.DisposeAsync();
        }
    }
}
