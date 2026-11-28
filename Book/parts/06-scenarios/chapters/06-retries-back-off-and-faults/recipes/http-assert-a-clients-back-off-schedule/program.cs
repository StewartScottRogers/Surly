// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a client's back-off: the server answers 503 three times, the client waits longer each
// time, and the test reads when each attempt reached the server from the server's own record.
// Run: dotnet run program.cs [-- <case>]
//   (none)    a client that doubles its wait: 250, 500 and 1000 ms
//   flat      a client that waits 250 ms every time, checked against the doubling design
//   record    what the record holds for each attempt
//   gaps      each gap against its range
//   summary   the retry count and the final status
//   faults    what the server recorded about the failures
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "doubling";

// The server: /hello.txt answers 503 for its first three requests, then serves the file.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["warmup.txt"] = "warm\n"u8.ToArray();
options.Faults.Add(SurlFault.ProtocolError("503") with { Scheme = "http", Path = "/hello.txt", Times = 3 });
await using SurlServer server = await SurlServer.StartAsync(options);

// One untimed request first: the first one in a process takes seconds to set up, and would show as a long gap.
await Fetch("warmup.txt");
var ended = new SurlEventQuery { Scheme = "http", Kind = SurlEventKind.ExchangeEnded };
long seen = (await server.WaitForEventAsync(ended, TimeSpan.FromSeconds(30))).Sequence;

// The client under test: up to four attempts, waiting between them as designed.
TimeSpan[] design = [TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000)];
TimeSpan[] actual = which == "flat" ? [design[0], design[0], design[0]] : design;
var statuses = new List<int>();
for (int attempt = 0; attempt < 4; attempt++)
{
    // A new client for each attempt, so each attempt is a connection of its own and has its own start in the record.
    int status = await Fetch("hello.txt");
    statuses.Add(status);
    if (status == 200)
    {
        break;
    }
    await Task.Delay(actual[attempt]);
}
Console.WriteLine($"attempts: {string.Join(" ", statuses)}");

// The record: one exchange for each attempt. Wait, bounded, for each to be recorded as ended, then read the start times.
for (int i = 0; i < statuses.Count; i++)
{
    seen = (await server.WaitForEventAsync(ended with { AfterSequence = seen }, TimeSpan.FromSeconds(30))).Sequence;
}
List<DateTimeOffset> starts = server.Exchanges.OrderBy(e => e.ExchangeId).Skip(1).Select(e => e.StartedAt).ToList();

if (which == "summary")
{
    Console.WriteLine($"retries: {statuses.Count - 1}, final status: {statuses[^1]}");
    return 0;
}

if (which == "faults")
{
    var applied = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Cast<FaultAppliedEvent>().ToList();
    Console.WriteLine($"fault events: {applied.Count}, all on /hello.txt: {applied.All(e => e.Target == "/hello.txt")}");
    Console.WriteLine($"rule remaining: {server.Faults.List().Single().Remaining}");
    return 0;
}

if (which == "record")
{
    Console.WriteLine($"exchanges after the warm-up: {starts.Count}");
    Console.WriteLine($"start times increase: {starts.Zip(starts.Skip(1), (a, b) => b > a).All(x => x)}");
    return 0;
}

// The gaps between the starts of the attempts. A timer may fire a little early, so a gap is allowed 50 ms under its design.
TimeSpan[] gaps = starts.Zip(starts.Skip(1), (a, b) => b - a).ToArray();
TimeSpan slack = TimeSpan.FromMilliseconds(50);
var short_ = Enumerable.Range(0, gaps.Length).Where(i => gaps[i] < design[i] - slack).Select(i => i + 1).ToList();
Console.WriteLine($"gaps below their design: {(short_.Count == 0 ? "none" : string.Join(" ", short_))}");
if (which == "gaps")
{
    for (int i = 0; i < gaps.Length; i++)
    {
        Console.WriteLine($"gap {i + 1} in range: {gaps[i] >= design[i] - slack && gaps[i] < design[i] + TimeSpan.FromSeconds(5) + slack}");
    }
}
if (which == "doubling")
{
    Console.WriteLine($"last gap longer than the first: {gaps[^1] > gaps[0]}");
    Console.WriteLine($"every gap under its design plus 5 s: {Enumerable.Range(0, gaps.Length).All(i => gaps[i] < design[i] + TimeSpan.FromSeconds(5))}");
}
return 0;

async Task<int> Fetch(string path)
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode;
}
