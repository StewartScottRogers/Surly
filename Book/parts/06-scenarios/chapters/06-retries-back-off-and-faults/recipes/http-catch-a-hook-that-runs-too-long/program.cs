// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A response override that runs too long: the server stops waiting for it, serves the request as
// normal and publishes a failure event the test asserts on. Server and client are in this process.
// Run: dotnet run program.cs [-- <case>]
//   (none)    a decision that waits 30 seconds, under a 300 ms limit
//   token     whether the decision's token is cancelled at the limit
//   twice     the same slow override on two requests
//   in-time   a decision slower than nothing but inside its limit
//   range     the limit's own bounds
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Diagnostics;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "slow";
TimeSpan limit = which == "in-time" ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(300);

if (which == "range")
{
    foreach (TimeSpan bad in new[] { TimeSpan.FromSeconds(31), TimeSpan.Zero })
    {
        try
        {
            await using var refused = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, HookTimeLimit = bad });
            Console.WriteLine($"{bad}: started");
        }
        catch (ArgumentException failure)
        {
            Console.WriteLine($"{bad}: {failure.Message}");
        }
    }
    await using var longest = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, HookTimeLimit = TimeSpan.FromSeconds(30) });
    Console.WriteLine("00:00:30: started");
    return 0;
}

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true, HookTimeLimit = limit };
options.Files["report.txt"] = "totals\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
var failed = new SurlEventQuery { Kind = SurlEventKind.HookFailed };
long last = 0;   // the sequence number of the last failure event the test has read

// One untimed request first: the first one in a process takes seconds to set up, and no timed case may include it.
await Get();

// The decision: it waits as long as asked, unless the server cancels its token at the limit.
var cancelled = new TaskCompletionSource();
TimeSpan wait = which == "in-time" ? TimeSpan.FromMilliseconds(300) : TimeSpan.FromSeconds(30);
server.Faults.AddOverride(new SurlFaultScope { Scheme = "http", Stage = FaultStage.Command }, async (point, cancel) =>
{
    try
    {
        await Task.Delay(wait, cancel);
    }
    catch (OperationCanceledException)
    {
        cancelled.TrySetResult();
        throw;
    }
    return SurlFault.ProtocolError("503");
});

switch (which)
{
    case "slow":
        (string served, TimeSpan took) = await Timed();
        Console.WriteLine(served);
        // Ranges, not exact times: at least the limit, and far less than the decision's 30 seconds.
        Console.WriteLine($"took at least the limit: {took >= limit - TimeSpan.FromMilliseconds(50)}; took less than 10 s: {took < TimeSpan.FromSeconds(10)}");
        Console.WriteLine(await Failure());
        break;

    case "token":
        await Get();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Console.WriteLine("the decision's token was cancelled");
        break;

    case "twice":
        Console.WriteLine(await Get());
        await Failure();
        Console.WriteLine(await Get());
        await Failure();
        Console.WriteLine($"failure events: {server.FindEvents(failed).Count}");
        break;

    case "in-time":
        (string answered, TimeSpan inTime) = await Timed();
        Console.WriteLine(answered);
        Console.WriteLine($"took at least the decision's wait: {inTime >= wait - TimeSpan.FromMilliseconds(50)}");
        Console.WriteLine($"failure events: {server.FindEvents(failed).Count}");
        break;
}
return 0;

async Task<string> Get()
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}report.txt"), CancellationToken.None);
    await using Stream body = response.Body;
    return $"GET /report.txt: {response.StatusCode} {(await new StreamReader(body).ReadToEndAsync()).Trim()}";
}

async Task<(string, TimeSpan)> Timed()
{
    var clock = Stopwatch.StartNew();
    string text = await Get();
    return (text, clock.Elapsed);
}

// Wait, bounded, for the next failure event after the last one read.
async Task<string> Failure()
{
    var hook = (HookFailedEvent)await server.WaitForEventAsync(failed with { AfterSequence = last }, TimeSpan.FromSeconds(30));
    last = hook.Sequence;
    return $"{hook.Failure}: {hook.Message}";
}
