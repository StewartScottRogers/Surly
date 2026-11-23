// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A response override that fails: the request is still served, and the test reads the failure
// as an event from the server. The server and the client are both in this process.
// Run: dotnet run program.cs [-- <case>]
//   (none)    a decision that throws
//   twice     the same override on two requests
//   refused   decisions that answer with a fault the server cannot apply
//   next      a failing override followed by one that answers 503
//   quiet     a decision that answers null
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "throws";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["report.txt"] = "totals\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
var scope = new SurlFaultScope { Scheme = "http", Stage = FaultStage.Command };
var failed = new SurlEventQuery { Kind = SurlEventKind.HookFailed };
long last = 0;   // the sequence number of the last failure event the test has read

// One untimed request first: the first one in a process takes seconds to set up.
await Get();

switch (which)
{
    case "throws":
        string id = server.Faults.AddOverride(scope, (point, cancel) => throw new InvalidOperationException("the price list is not loaded"));
        Console.WriteLine(await Get());
        HookFailedEvent hook = await Next();
        Console.WriteLine($"hook {hook.HookId} is override {id}: {hook.HookId == id}; {hook.Failure}, {hook.Message}");
        break;

    case "twice":
        server.Faults.AddOverride(scope, (point, cancel) => throw new InvalidOperationException("boom"));
        Console.WriteLine(await Get());
        await Failure();
        Console.WriteLine(await Get());
        await Failure();
        Console.WriteLine($"failure events: {server.FindEvents(failed).Count}");
        break;

    case "refused":
        foreach (SurlFault answer in new[] { SurlFault.RefuseLogin(), SurlFault.ProtocolError("abc") })
        {
            string one = server.Faults.AddOverride(scope, (point, cancel) => ValueTask.FromResult(answer));
            Console.WriteLine(await Get());
            Console.WriteLine($"{answer.Kind}: {await Failure()}");
            server.Faults.Remove(one);
        }
        break;

    case "next":
        server.Faults.AddOverride(scope, (point, cancel) => throw new InvalidOperationException("boom"));
        server.Faults.AddOverride(scope, (point, cancel) => ValueTask.FromResult(SurlFault.ProtocolError("503")));
        Console.WriteLine(await Get());
        Console.WriteLine(await Failure());
        break;

    case "quiet":
        server.Faults.AddOverride(scope, (point, cancel) => ValueTask.FromResult<SurlFault>(null!));
        Console.WriteLine(await Get());
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

// Wait, bounded, for the next failure event after the last one seen.
async Task<HookFailedEvent> Next()
{
    var hook = (HookFailedEvent)await server.WaitForEventAsync(failed with { AfterSequence = last }, TimeSpan.FromSeconds(30));
    last = hook.Sequence;
    return hook;
}

async Task<string> Failure()
{
    HookFailedEvent hook = await Next();
    return $"{hook.Failure}, {hook.Message}";
}
