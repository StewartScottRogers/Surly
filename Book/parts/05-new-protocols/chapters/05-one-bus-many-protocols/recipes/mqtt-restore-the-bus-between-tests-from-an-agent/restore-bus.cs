// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent puts the message bus back between tests: surl-mcp runs an MQTT broker and an AMQP server on
// one bus, seeds a retained message and two queued jobs, snapshots that once, lets a test change it
// with curl-mcp's curl tool, and restores the snapshot before the next test.
// Run: dotnet run restore-bus.cs -- <case>
//   restore   a test, then restore_server (the default)
//   dirty     a test and no restore: what the next test would find
//   twice     a test, a restore, a second test, the same restore again
//   reset     a test, then reset_server in place of the restore
//   unknown   restore_server with a snapshot never taken
//   locked    the same server without --allow-anonymous: the test's first publish
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "restore";

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

// MQTT and AMQP on one bus: the queue jobs, and every message under devices/ kept.
var arguments = new List<string> { "--queue", "jobs", "--topic", "devices/#;retain" };
if (mode != "locked")
{
    arguments.Add("--allow-anonymous");
}

JsonElement started = await Surl("start_server", new() { ["listen_urls"] = new[] { "mqtt://127.0.0.1:0", "amqp://127.0.0.1:0" }, ["arguments"] = arguments });
string id = started.GetProperty("server_id").GetString()!;
string mqtt = started.GetProperty("bound_urls")[0].GetString()!;
string amqp = started.GetProperty("bound_urls")[1].GetString()!;
long last = 0;

// The state every test starts from, seeded once and snapshotted once.
await Surl("publish_message", new() { ["server_id"] = id, ["topic"] = "devices/lamp/state", ["payload"] = "off", ["retain"] = true });
await Surl("publish_message", new() { ["server_id"] = id, ["queue"] = "jobs", ["payload"] = "job-1" });
await Surl("publish_message", new() { ["server_id"] = id, ["queue"] = "jobs", ["payload"] = "job-2" });
string snapshot = (await Surl("snapshot_server", new() { ["server_id"] = id })).GetProperty("snapshot_id").GetString()!;

if (mode == "locked")
{
    JsonElement refused = await Curl("-s", "-S", "-d", "on", mqtt + "devices/lamp/state");
    Console.WriteLine($"publish refused: exit {refused.GetProperty("exitCode").GetInt32()}, {refused.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd()}");
    return 1;
}

await RunTest();
switch (mode)
{
    case "restore":
        await Surl("restore_server", new() { ["server_id"] = id, ["snapshot_id"] = snapshot });
        await Print($"after restore_server {snapshot}");
        break;
    case "dirty":
        await Print("after the test");
        break;
    case "twice":
        await Surl("restore_server", new() { ["server_id"] = id, ["snapshot_id"] = snapshot });
        await RunTest();
        await Surl("restore_server", new() { ["server_id"] = id, ["snapshot_id"] = snapshot });
        await Print($"after a second test and restore_server {snapshot} again");
        break;
    case "reset":
        await Surl("reset_server", new() { ["server_id"] = id });
        await Print("after reset_server");
        break;
    case "unknown":
        CallToolResult failed = await surl.CallToolAsync("restore_server", new Dictionary<string, object?> { ["server_id"] = id, ["snapshot_id"] = "p9" });
        Console.WriteLine($"tool error {failed.IsError}: {failed.Content.OfType<TextContentBlock>().Single().Text}");
        return 1;
}

return 0;

// A test that changes the bus: it turns the lamp on, takes a job over AMQP and adds a device.
async Task RunTest()
{
    await Curl("-s", "-S", "-d", "on", mqtt + "devices/lamp/state");
    await Curl("-s", "-S", amqp + "%2F/jobs");
    await Curl("-s", "-S", "-d", "on", mqtt + "devices/hall/state");
}

// What the next test would find: two retained topics and the queue.
async Task Print(string when)
{
    Console.WriteLine(when + ":");
    foreach (string topic in new[] { "devices/lamp/state", "devices/hall/state" })
    {
        JsonElement kept = await Surl("read_retained_message", new() { ["server_id"] = id, ["topic"] = topic });
        Console.WriteLine($"  {topic}: {(kept.TryGetProperty("text", out JsonElement text) ? text.GetString() : "(nothing)")}");
    }

    JsonElement queued = await Surl("browse_queue", new() { ["server_id"] = id, ["queue"] = "jobs" });
    Console.WriteLine($"  jobs: [{string.Join(", ", queued.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("text").GetString()))}]");
}

// A surl-mcp tool's structured answer.
async Task<JsonElement> Surl(string tool, Dictionary<string, object?> arguments)
{
    CallToolResult result = await surl.CallToolAsync(tool, arguments);
    return result.StructuredContent!.Value;
}

// The curl tool runs curl's own arguments; a publish is not acknowledged, so the agent then waits,
// at most ten seconds, for the server to end the exchange.
async Task<JsonElement> Curl(params string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    JsonElement ended = await Surl("wait_for_event", new() { ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["after_sequence"] = last, ["timeout_ms"] = 10000 });
    last = ended.GetProperty("event").GetProperty("sequence").GetInt64();
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}
