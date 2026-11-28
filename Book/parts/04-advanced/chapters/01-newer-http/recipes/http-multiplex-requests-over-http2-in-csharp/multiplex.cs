// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches five files at once with one CurlClient over HTTP/2 with prior knowledge, from a
// Surl.Api server that records its exchanges, and counts the connections the server saw.
// Run: dotnet run multiplex.cs [-- separate | slow | http1.1]
//   (no argument) PipeWait on: every request waits for a connection it can share
//   separate      PipeWait off: a request that finds no connection ready opens its own
//   slow          the server holds every request 2 s, not 1 s; times the five together, then one after another
//   http1.1       PipeWait on, but HTTP/1.1, which cannot share a connection
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Diagnostics;
using Curl.Api;
using Curl.Api.Options.Http;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "";

// The server: five small files in memory, every exchange recorded.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
for (int i = 1; i <= 5; i++)
{
    options.Files[$"f{i}.txt"] = System.Text.Encoding.UTF8.GetBytes($"file {i}\n");
}

// Every request is held before it is answered, 1 s (2 s with slow), so the five are all in flight
// together and none can find a connection another has finished with.
options.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(mode == "slow" ? 2 : 1)) with { Stage = FaultStage.Command });

await using var server = await SurlServer.StartAsync(options);

// The first Curl.Api request in a process pays a one-time set-up; pay it on a client of its own.
await using (CurlClient warmUp = CurlClient.Create())
{
    CurlResponse first = await warmUp.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}f1.txt"), CancellationToken.None);
    await first.Body.DisposeAsync();
}

int before = server.Exchanges.Count;

// The client: five requests started together on one CurlClient.
var http = new CurlHttpOptions
{
    HttpVersion = mode == "http1.1" ? HttpVersion.Http11 : HttpVersion.Http2PriorKnowledge,
    PipeWait = mode != "separate",
};
await using CurlClient client = CurlClient.Create();
var clock = Stopwatch.StartNew();
string[] lines = await Task.WhenAll(Enumerable.Range(1, 5).Select(Get));
clock.Stop();

foreach (string line in lines)
{
    Console.WriteLine(line);
}

// One exchange is one connection the server accepted.
Console.WriteLine($"connections: {server.Exchanges.Count - before}");
if (mode == "slow")
{
    // The same five again, one after another: the comparison of the two runs is the proof, not a fixed bound.
    var apart = Stopwatch.StartNew();
    for (int i = 1; i <= 5; i++)
    {
        await Get(i);
    }

    apart.Stop();
    Console.WriteLine($"took {(int)clock.Elapsed.TotalSeconds} s");
    Console.WriteLine($"one after another: {(int)apart.Elapsed.TotalSeconds} s");
    Console.WriteLine($"together took less: {(clock.Elapsed < apart.Elapsed ? "yes" : "no")}");
}

return 0;

async Task<string> Get(int i)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}f{i}.txt") { Http = http };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    return $"f{i}.txt: HTTP/{response.Version} {response.StatusCode} {text.TrimEnd()}";
}
