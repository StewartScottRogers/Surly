// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tunes a client's retry policy by experiment: for a server that fails its first N
// requests with 503, N growing from 1 to 3, it tries retry counts and delays with the curl tool,
// cheapest first, and recommends the smallest policy that still succeeds.
// Run: dotnet run tune.cs [-- <case>]
//   (no argument)  the sweep over N = 1, 2, 3 and the recommendation
//   events         one trial, then the fault events the server recorded for it
//   no-fail        a trial without -f: the last 503 exits 0
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "sweep";

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

// The service under test: one file on a free port.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["status.json"] = """{"ok":true}""" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "status.json";

if (which == "no-fail")
{
    await Outage(2);
    Console.WriteLine($"2 failures, --retry 1 without -f: {await Trial(1, 1, fail: false)}");
    return 0;
}

if (which == "events")
{
    long before = await LastSequence();
    await Outage(2);
    Console.WriteLine($"2 failures, --retry 2 --retry-delay 1: {await Trial(2, 1)}");
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "fault_applied" }, ["after_sequence"] = before,
    });
    foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
    {
        Console.WriteLine($"fault_applied: {e.GetProperty("fault_kind").GetString()} on {e.GetProperty("target").GetString()}");
    }

    return 0;
}

// The policies to try, cheapest first: by the seconds spent waiting between tries, then by tries.
(int Retries, int Delay)[] policies = [(0, 1), (1, 1), (1, 2), (2, 1), (3, 1), (2, 2), (3, 2)];
(int Retries, int Delay) recommended = (0, 0);
for (int failures = 1; failures <= 3; failures++)
{
    foreach ((int retries, int delay) in policies)
    {
        await Outage(failures);
        string result = await Trial(retries, delay);
        Console.WriteLine($"{failures} failures, --retry {retries} --retry-delay {delay}: {result}");
        if (result.StartsWith("exit 0"))
        {
            recommended = (Math.Max(recommended.Retries, retries), Math.Max(recommended.Delay, delay));
            break;
        }
    }
}

Console.WriteLine($"recommended: --retry {recommended.Retries} --retry-delay {recommended.Delay}, at most {recommended.Retries * recommended.Delay} s of waiting");
return 0;

// A fresh outage: remove the last trial's rule, then fail the next N requests with 503.
async Task Outage(int failures)
{
    CallToolResult listed = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = id });
    foreach (JsonElement rule in listed.StructuredContent!.Value.GetProperty("faults").EnumerateArray())
    {
        await surl.CallToolAsync("remove_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault_id"] = rule.GetProperty("fault_id").GetString() });
    }

    await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault"] = $"error;scheme=http;reply=503;times={failures}" });
}

// One trial of a policy with the curl tool: its exit code, the last status and the retries made.
async Task<string> Trial(int retries, int delay, bool fail = true)
{
    string[] argv = ["-s", "-S", "--max-time", "30", "--retry", $"{retries}", "--retry-delay", $"{delay}", "-o", "status.out", "-w", "%{http_code} %{num_retries}", url];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = fail ? ["-f", .. argv] : argv });
    using JsonDocument doc = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    string[] w = doc.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim().Split(' ');
    return $"exit {doc.RootElement.GetProperty("exitCode").GetInt32()}, status {w[0]} after {w[1]} retries";
}

// The sequence number of the server's last event, so a later get_events reads only what follows.
async Task<long> LastSequence()
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id });
    return events.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("sequence").GetInt64()).DefaultIfEmpty(0).Max();
}
