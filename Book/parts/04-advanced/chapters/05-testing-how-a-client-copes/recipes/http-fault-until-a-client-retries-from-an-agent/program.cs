// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp server answer 503 until it has seen a client meet the fault, then takes
// the fault away and checks the client's retry got the file. Run: dotnet run program.cs -- <case>
//   solution  add the fault, start the client, wait for the event, remove the fault
//   kept      the fault is never removed
//   not-met   the client asks for a file the rule is not for, and the agent waits
//   removed   transfer before and after remove_fault
//   unknown-id  remove_fault of an ID the server has not given
//   scoped    the fault is for /report.txt and the client asks for /health.txt
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
    ["files"] = new Dictionary<string, string> { ["report.txt"] = "report ready\n", ["health.txt"] = "up\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

File.Delete(Path.Combine(here, "got.txt"));

// The first request curl-mcp makes has one-time set-up costs: make one before the test starts.
await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = baseUrl + "health.txt" });

// The rule has no times, so it acts on every request for /report.txt until it is removed.
CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
{
    ["server_id"] = server, ["fault"] = "error;scheme=http;reply=503;path=/report.txt",
});
string fault = added.StructuredContent!.Value.GetProperty("fault_id").GetString()!;
Console.WriteLine($"add_fault: {fault}");

if (which == "removed")
{
    // One attempt at a time: the rule answers 503, and once it is removed the same request is served.
    Console.WriteLine($"transfer before remove_fault: {await Status()}");
    await surl.CallToolAsync("remove_fault", new Dictionary<string, object?> { ["server_id"] = server, ["fault_id"] = fault });
    Console.WriteLine($"transfer after remove_fault: {await Status()}");
    return 0;
}

if (which == "unknown-id")
{
    CallToolResult refused = await surl.CallToolAsync("remove_fault", new Dictionary<string, object?> { ["server_id"] = server, ["fault_id"] = "f9" });
    Console.WriteLine($"remove_fault f9: tool error {(refused.IsError == true ? "yes" : "no")}, {refused.Content.OfType<TextContentBlock>().Single().Text}");
    return 0;
}

// The client: curl with up to five retries, a second apart, started and not awaited. -f makes a 503 a failure.
string path = which is "scoped" or "not-met" ? "health.txt" : "report.txt";
string[] clientArgs = ["-s", "-f", "--retry", which == "kept" ? "2" : "5", "--retry-delay", "1", "-o", "got.txt", "-w", "%{http_code} %{num_retries}", baseUrl + path];
Task<CallToolResult> client = curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = clientArgs }).AsTask();

if (which == "not-met")
{
    // The client asks for a file the rule is not for: the agent waits, and nothing is there to see.
    JsonElement waited = (await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "fault_applied" }, ["timeout_ms"] = 1500,
    })).StructuredContent!.Value;
    Console.WriteLine($"wait_for_event: timed_out {waited.GetProperty("timed_out").GetRawText()}");
}

if (which == "solution")
{
    // Wait, with a bound, for the event that says the client met the fault, then take the rule away.
    JsonElement waited = (await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "fault_applied" }, ["timeout_ms"] = 10000,
    })).StructuredContent!.Value;
    JsonElement met = waited.GetProperty("event");
    Console.WriteLine($"wait_for_event: {met.GetProperty("kind").GetString()} {met.GetProperty("rule_id").GetString()} {met.GetProperty("target").GetString()}");
    await surl.CallToolAsync("remove_fault", new Dictionary<string, object?> { ["server_id"] = server, ["fault_id"] = fault });
    Console.WriteLine($"remove_fault: {fault}");
}

JsonElement answer = JsonDocument.Parse((await client).Content.OfType<TextContentBlock>().Single().Text).RootElement;
string[] printed = answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Split(' ');
Console.WriteLine($"curl: exit {answer.GetProperty("exitCode")}, status {printed[0]}, retried at least once: {int.Parse(printed[1]) >= 1}");
if (File.Exists(Path.Combine(here, "got.txt")) && which != "kept")
{
    Console.Write($"got.txt: {File.ReadAllText(Path.Combine(here, "got.txt"))}");
}

// The server's record: how many times the rule acted, and which rules are left.
JsonElement applied = (await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
})).StructuredContent!.Value;
int acted = applied.GetProperty("events").GetArrayLength();
Console.WriteLine(which == "solution" ? $"fault_applied: at least once, at most five: {acted is >= 1 and <= 5}" : $"fault_applied: {acted}");
JsonElement listed = (await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server })).StructuredContent!.Value;
Console.WriteLine($"list_faults: {listed.GetProperty("faults").GetArrayLength()} rules");
return 0;

// The status of one request for the faulted file, through transfer: one attempt, no retry.
async Task<int> Status()
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = baseUrl + "report.txt" });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("statusCode").GetInt32();
}
