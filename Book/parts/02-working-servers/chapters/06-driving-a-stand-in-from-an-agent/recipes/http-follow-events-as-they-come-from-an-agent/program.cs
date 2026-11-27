// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent follows a surl-mcp server's events a page at a time, keeping the sequence number of the
// last event it has seen and asking only for what came after it. Run: dotnet run program.cs -- <case>
//   solution   three requests, then the events read in pages of four from the first
//   after      a fourth request, then only the events after the last one seen
//   quiet      nothing new: an empty page, and a wait that times out
//   wait       a wait for what comes after the last event seen, sent before the request
//   kinds      a filter on kind together with the cursor
//   one-at-a-time  pages of one event
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();
await using McpClient surl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl-mcp", Command = "surl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["SURL_MCP_ROOT"] = here },
}));
await using McpClient curl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "curl-mcp", Command = "curl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["CURL_MCP_ROOT"] = here },
}));

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Three clients, one after the other. Each one's exchange has ended in the record before the next starts.
for (int i = 1; i <= 3; i++)
{
    await Fetch(i);
}

// The agent's cursor: the sequence number of the last event it has seen, 0 before the first.
long last = 0;
List<long> seen = [];
if (which == "solution")
{
    for (int page = 1; ; page++)
    {
        JsonElement answer = await Events(new() { ["after_sequence"] = last, ["max_events"] = 4 });
        JsonElement[] events = answer.GetProperty("events").EnumerateArray().ToArray();
        Console.WriteLine($"page {page}: {Letters(events)}, has_more {answer.GetProperty("has_more").GetRawText()}");
        seen.AddRange(events.Select(e => e.GetProperty("sequence").GetInt64()));
        last = seen[^1];
        if (!answer.GetProperty("has_more").GetBoolean()) break;
    }

    // The pages together are the whole record, once each and in order.
    List<long> whole = (await Events(new() { ["after_sequence"] = 0 })).GetProperty("events").EnumerateArray().Select(e => e.GetProperty("sequence").GetInt64()).ToList();
    Console.WriteLine($"pages together are the whole record, in order, no repeats: {seen.SequenceEqual(whole) && seen.Distinct().Count() == seen.Count && seen.SequenceEqual(seen.Order())}");
    return 0;
}

// The other cases start from a cursor at the end of the three requests.
last = (await Events(new())).GetProperty("events").EnumerateArray().Max(e => e.GetProperty("sequence").GetInt64());

if (which == "after")
{
    await Fetch(4);
    JsonElement answer = await Events(new() { ["after_sequence"] = last });
    JsonElement[] events = answer.GetProperty("events").EnumerateArray().ToArray();
    Console.WriteLine($"after the last event seen: {Letters(events)}, has_more {answer.GetProperty("has_more").GetRawText()}");
    Console.WriteLine($"all after it: {events.All(e => e.GetProperty("sequence").GetInt64() > last)}");
}
else if (which == "quiet")
{
    JsonElement answer = await Events(new() { ["after_sequence"] = last });
    Console.WriteLine($"after the last event seen: {answer.GetProperty("events").GetArrayLength()} events, has_more {answer.GetProperty("has_more").GetRawText()}");
    JsonElement waited = (await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_sequence"] = last, ["timeout_ms"] = 300,
    })).StructuredContent!.Value;
    Console.WriteLine($"wait_for_event after it: timed_out {waited.GetProperty("timed_out").GetRawText()}");
}
else if (which == "kinds")
{
    // The cursor and the filter work together: only the kinds asked for, only after the cursor.
    JsonElement[] all = (await Events(new() { ["after_sequence"] = 0, ["kinds"] = new[] { "exchange_ended" } })).GetProperty("events").EnumerateArray().ToArray();
    Console.WriteLine($"exchange_ended from the start: {Letters(all)}");
    JsonElement[] rest = (await Events(new() { ["after_sequence"] = all[0].GetProperty("sequence").GetInt64(), ["kinds"] = new[] { "exchange_ended" } })).GetProperty("events").EnumerateArray().ToArray();
    Console.WriteLine($"exchange_ended after the first of them: {Letters(rest)}");
}
else if (which == "one-at-a-time")
{
    // A page of one event: six calls, each with a more to come but the last.
    long cursor = 0;
    List<bool> more = [];
    while (true)
    {
        JsonElement answer = await Events(new() { ["after_sequence"] = cursor, ["max_events"] = 1 });
        JsonElement[] events = answer.GetProperty("events").EnumerateArray().ToArray();
        if (events.Length == 0) break;
        cursor = events[0].GetProperty("sequence").GetInt64();
        more.Add(answer.GetProperty("has_more").GetBoolean());
    }

    Console.WriteLine($"pages of one event: {more.Count}, has_more true on {more.Count(m => m)} of them, false on the last: {!more[^1]}");
}
else if (which == "wait")
{
    // The wait is sent first; the fourth request makes the event it answers.
    Task<CallToolResult> waiting = surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_sequence"] = last, ["timeout_ms"] = 10000,
    }).AsTask();
    await Fetch(4);
    JsonElement e = (await waiting).StructuredContent!.Value.GetProperty("event");
    Console.WriteLine($"wait_for_event: {e.GetProperty("kind").GetString()}, after the last event seen: {e.GetProperty("sequence").GetInt64() > last}");
}

return 0;

// One request through curl-mcp, then wait, with a bound, until the record holds i ended exchanges.
async Task Fetch(int i)
{
    await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt" });
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
    {
        var kinds = (await Events(new() { ["kinds"] = new[] { "exchange_ended" } })).GetProperty("events").GetArrayLength();
        if (kinds >= i) break;
    }
}

// get_events answers the events kept, oldest first, from after_sequence, at most max_events of them.
async Task<JsonElement> Events(Dictionary<string, object?> filters)
{
    filters["server_id"] = server;
    return (await surl.CallToolAsync("get_events", filters)).StructuredContent!.Value;
}

// Each event's kind as one letter: S for an exchange started, E for one ended.
static string Letters(IEnumerable<JsonElement> events) => string.Join(" ", events.Select(e => e.GetProperty("kind").GetString() == "exchange_started" ? "S" : "E"));
