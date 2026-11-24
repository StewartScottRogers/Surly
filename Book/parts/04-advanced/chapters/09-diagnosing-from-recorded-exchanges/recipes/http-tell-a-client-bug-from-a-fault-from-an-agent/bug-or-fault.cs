// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A client run fails in places while fault rules are set. Before blaming the client, the agent
// matches every failed exchange against surl-mcp's fault_applied events.
// Run: dotnet run bug-or-fault.cs
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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
    ["files"] = new Dictionary<string, string> { ["report.txt"] = "report\n", ["status.txt"] = "ok\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The fault rules the test set: the first request for /report.txt gets 503, and the first
// connection that asks for /status.txt is dropped without an answer.
foreach (string fault in new[] { "error;scheme=http;path=/report.txt;reply=503;times=1", "drop;stage=command;path=/status.txt;times=1" })
{
    CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = serverId, ["fault"] = fault });
    Console.WriteLine($"add_fault: {added.StructuredContent!.Value.GetProperty("fault_id")}");
}

// The client run: one curl tool call per request, so one exchange per request. One path has a typo.
foreach (string path in new[] { "report.txt", "report.txt", "status.txt", "reports.txt", "status.txt" })
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", "--max-time", "30", "-o", "out.txt", "-w", "%{http_code}", baseUrl + path } });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"client: /{path} {run.GetProperty("standardOutput").GetProperty("content").GetString()}, exitCode {run.GetProperty("exitCode").GetInt32()}");
}

// Wait, at most 10 s, until every exchange that started has ended, so the record is whole.
async Task<List<JsonElement>> Events(string kind)
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = serverId, ["kinds"] = new[] { kind } });
    return events.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToList();
}
for (var clock = System.Diagnostics.Stopwatch.StartNew(); (await Events("exchange_ended")).Count < (await Events("exchange_started")).Count;)
{
    if (clock.Elapsed > TimeSpan.FromSeconds(10)) { Console.WriteLine("the exchanges did not end within 10 s"); return 1; }
    await Task.Delay(100);
}

// The fault events, by the exchange each acted on.
Dictionary<long, JsonElement> faults = (await Events("fault_applied")).ToDictionary(e => e.GetProperty("exchange_id").GetInt64());

// Every exchange: its request, its answer (or none), and whether a fault rule acted on it.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
foreach (JsonElement exchange in recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    long id = exchange.GetProperty("exchange_id").GetInt64();
    var chunks = exchange.GetProperty("chunks").EnumerateArray().ToList();
    string request = chunks.First(c => c.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!.Split("\r\n")[0];
    JsonElement sent = chunks.FirstOrDefault(c => c.GetProperty("direction").GetString() == "sent");
    string answer = sent.ValueKind == JsonValueKind.Undefined ? "no answer" : sent.GetProperty("data").GetString()!.Split(' ')[1];
    if (answer == "200") continue;
    string verdict = faults.TryGetValue(id, out JsonElement f)
        ? $"fault rule {f.GetProperty("rule_id")} ({f.GetProperty("fault_kind")}) acted on it: not the client"
        : "no fault rule acted on it: look at the client";
    Console.WriteLine($"exchange {id}: {request} -> {answer}; {verdict}");
}

// The rules are spent: each had times=1 and acted once.
CallToolResult listed = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = serverId });
Console.WriteLine($"list_faults remaining: {string.Join(" ", listed.StructuredContent!.Value.GetProperty("faults").EnumerateArray().Select(r => $"{r.GetProperty("fault_id")}={r.GetProperty("remaining")}"))}");
return 0;
