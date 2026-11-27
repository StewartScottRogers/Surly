// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- SCENARIO`. Package versions are the book's pins in
// Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Diagnostics;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

// The scenario picks the client's cap on connections to one host (0 is no cap), how many files to
// fetch, and whether to start the requests together or one after another.
(int cap, int count, bool together) = args[0] switch
{
    "capped" => (4, 12, true),
    "uncapped" => (0, 12, true),
    "serial" => (0, 3, false),
    "cap-of-one" => (1, 3, true),
    _ => throw new ArgumentException(args[0]),
};

// The server, in-process, recording its events: twelve small files, every answer held 1 s so requests
// that run together overlap.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
for (int i = 1; i <= 12; i++) options.Files[$"f{i}.txt"] = System.Text.Encoding.UTF8.GetBytes($"file {i}\n");
options.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(1)) with { Stage = FaultStage.Command });
await using var server = await SurlServer.StartAsync(options);

// The first Curl.Api request in a process pays a one-time set-up; pay it against a server of its own,
// so the measured server sees only the fetches.
await using (var other = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } }))
await using (CurlClient warmUp = CurlClient.Create())
{
    CurlResponse first = await warmUp.SendAsync(new CurlRequest(HttpMethod.Get, $"{other.BoundUrl}x"), CancellationToken.None);
    await first.Body.DisposeAsync();
}

async Task<bool> Fetch(CurlClient client, int i)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}f{i}.txt"), CancellationToken.None);
    await using Stream body = response.Body;
    return response.StatusCode == 200 && await new StreamReader(body).ReadToEndAsync() == $"file {i}\n";
}

// MaxConnectionsPerHost is --parallel-max's spelling: with 4, a fifth request waits for a free connection.
var clock = Stopwatch.StartNew();
bool[] ok;
await using (CurlClient client = CurlClient.Create(new CurlClientOptions { MaxConnectionsPerHost = cap }))
{
    if (together) ok = await Task.WhenAll(Enumerable.Range(1, count).Select(i => Fetch(client, i)));
    else { ok = new bool[count]; for (int i = 0; i < count; i++) ok[i] = await Fetch(client, i + 1); }
}
clock.Stop();

// The server's record: an exchange is one connection. Wait (at most 10 s) until every one has ended,
// then walk the events in order, counting up at a start and down at an end.
List<(long Sequence, int Step)> steps = [];
for (int tries = 0; tries < 100; tries++)
{
    var started = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted });
    var ended = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded });
    steps = started.Select(e => ((long)e.Sequence, 1)).Concat(ended.Select(e => ((long)e.Sequence, -1))).OrderBy(x => x.Item1).ToList();
    if (started.Count == ended.Count) break;
    await Task.Delay(100);
}
int open = 0, peak = 0;
foreach (var (_, step) in steps)
{
    open += step;
    peak = Math.Max(peak, open);
}
Console.WriteLine($"{ok.Count(x => x)} of {count} fetched");
Console.WriteLine($"connections {peak}");
Console.WriteLine($"took {(int)clock.Elapsed.TotalSeconds} s");
return 0;
