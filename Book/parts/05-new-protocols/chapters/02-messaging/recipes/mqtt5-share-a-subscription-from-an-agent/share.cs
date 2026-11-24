// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks a shared MQTT 5 subscription: surl-mcp runs the broker, three curl tool calls subscribe (two
// workers in the group $share/workers/jobs and a monitor on jobs), and once get_exchanges shows all three
// subscribed, six jobs are published and the agent counts what each was sent.
// Run: dotnet run share.cs -- <case>
//   share         two workers share the jobs; the monitor is sent them all (the default)
//   late          the same, then a worker and a plain subscriber that come late
//   no-anonymous  a worker with no login, on a broker without --allow-anonymous
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "share";

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

// An MQTT 5 broker that keeps the last message on jobs.
string[] options = mode == "no-anonymous" ? ["--topic", "jobs;retain"] : ["--allow-anonymous", "--topic", "jobs;retain"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt5://127.0.0.1:0" },
    ["arguments"] = options,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

if (mode == "no-anonymous")
{
    JsonElement refused = await Curl("-s", "-S", "-m", "5", url + "%24share/workers/jobs");
    Console.WriteLine($"worker exit {refused.GetProperty("exitCode").GetInt32()}: {refused.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
    return 0;
}

// Three subscriptions, each a curl call that listens for at most 8 seconds. They run while the agent goes on.
Task<JsonElement> worker1 = Curl("-s", "-m", "8", url + "%24share/workers/jobs");
Task<JsonElement> worker2 = Curl("-s", "-m", "8", url + "%24share/workers/jobs");
Task<JsonElement> monitor = Curl("-s", "-m", "8", url + "jobs");

// All three are subscribed once the broker has sent three SUBACKs; ask a tenth of a second apart, at most 5 s.
for (int tries = 0; await Subscribed() < 3 && tries < 50; tries++)
{
    await Task.Delay(100);
}

// The app publishes six jobs, a line each.
for (int n = 1; n <= 6; n++)
{
    await Curl("-s", "-S", "-d", $"job-{n}\n", url + "jobs");
}

// When the subscriptions end, each one's output is what it was sent.
string[] jobs1 = Jobs(await worker1), jobs2 = Jobs(await worker2);
Console.WriteLine($"worker 1: {jobs1.Length} jobs");
Console.WriteLine($"worker 2: {jobs2.Length} jobs");
Console.WriteLine($"monitor: {Jobs(await monitor).Length} jobs");
Console.WriteLine($"between them: {string.Join(" ", jobs1.Concat(jobs2).Order())}");

if (mode == "late")
{
    JsonElement lateWorker = await Curl("-s", "-m", "3", url + "%24share/workers/jobs");
    JsonElement latePlain = await Curl("-s", "-m", "3", url + "jobs");
    Console.WriteLine($"late worker: exit {lateWorker.GetProperty("exitCode").GetInt32()}, [{string.Join(" ", Jobs(lateWorker))}]");
    Console.WriteLine($"late plain subscriber: exit {latePlain.GetProperty("exitCode").GetInt32()}, [{string.Join(" ", Jobs(latePlain))}]");
}

return 0;

// curl-mcp answers in text, as JSON, also when curl's exit is not 0.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

static string[] Jobs(JsonElement answer) =>
    answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Split('\n', StringSplitOptions.RemoveEmptyEntries);

// get_exchanges records the bytes each way; a SUBACK is a sent packet whose first byte is 0x90.
async Task<int> Subscribed()
{
    CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "mqtt5" });
    return got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Count(e => e.GetProperty("chunks").EnumerateArray().Any(c =>
    {
        string data = c.GetProperty("data").GetString()!;
        byte[] bytes = c.GetProperty("encoding").GetString() == "base64" ? Convert.FromBase64String(data) : Encoding.UTF8.GetBytes(data);
        return c.GetProperty("direction").GetString() == "sent" && bytes.Length > 0 && bytes[0] == 0x90;
    }));
}
