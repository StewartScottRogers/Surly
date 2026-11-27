// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A response override that decides each request: /report.txt fails with 503 only while the test
// says it is broken. The server and the client are both in this process.
// Run: dotnet run program.cs [-- <case>]
//   (none)    break /report.txt and mend it again between requests (the default)
//   event     the fault event the server records for the one failed request
//   point     what the decision is told about each request
//   remove    take the override away while the test still says broken
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Collections.Concurrent;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "flag";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["report.txt"] = "totals\n"u8.ToArray();
options.Files["other.txt"] = "other\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The test's condition: the paths it currently wants to fail. The decision reads it for each request.
var broken = new ConcurrentDictionary<string, bool>();
var seen = new ConcurrentQueue<string>();

// The override: asked at every http request, at the command stage. A fault answers; null leaves the request alone.
string id = server.Faults.AddOverride(
    new SurlFaultScope { Scheme = "http", Stage = FaultStage.Command },
    (point, cancel) =>
    {
        seen.Enqueue($"{point.Command} {point.Path}");
        SurlFault? answer = broken.ContainsKey(point.Path ?? "") ? SurlFault.ProtocolError("503") : null;
        return ValueTask.FromResult(answer!);
    });

// One untimed request first: the first one in a process takes seconds to set up.
await Get("other.txt");
seen.Clear();

switch (which)
{
    case "flag":
        Console.WriteLine(await Get("report.txt"));
        broken["/report.txt"] = true;
        Console.WriteLine(await Get("report.txt"));
        Console.WriteLine(await Get("other.txt"));
        broken.TryRemove("/report.txt", out _);
        Console.WriteLine(await Get("report.txt"));
        Console.WriteLine($"overrides in the fault plan's list: {server.Faults.List().Count}");
        break;

    case "event":
        broken["/report.txt"] = true;
        Console.WriteLine(await Get("report.txt"));
        Console.WriteLine(await Get("other.txt"));
        var applied = (FaultAppliedEvent)await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.FaultApplied }, TimeSpan.FromSeconds(30));
        int count = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count;
        Console.WriteLine($"fault events: {count}; {applied.Kind} at {applied.Stage} on {applied.Target}, rule {applied.RuleId} is override {id}: {applied.RuleId == id}");
        break;

    case "point":
        await Get("report.txt");
        await Get("other.txt");
        Console.WriteLine($"the decision was asked about: {string.Join(", ", seen)}");
        break;

    case "remove":
        broken["/report.txt"] = true;
        Console.WriteLine(await Get("report.txt"));
        Console.WriteLine($"removed: {server.Faults.Remove(id)}; again: {server.Faults.Remove(id)}");
        Console.WriteLine(await Get("report.txt"));
        break;
}
return 0;

// One request; the status line the test asserts on.
async Task<string> Get(string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = (await new StreamReader(body).ReadToEndAsync()).Trim();
    return $"GET /{path}: {response.StatusCode} {text}";
}
