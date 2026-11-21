// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a few devices against surl-mcp's MQTT broker, then checks the state each left
// behind by reading every topic's retained message, over the official MCP SDK.
// Run: dotnet run check-retained.cs -- <case>
//   ok         every device ends in the state expected (the default)
//   wrong      the heater is left on
//   silent     the pump never reports
//   outside    a device reports on a topic the broker was not told to keep
//   no-retain  a broker started without retain
//   refused    a broker started without --allow-anonymous
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "ok";

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

// surl-mcp: an MQTT broker that keeps what is published under devices/ and lets devices in without a login.
var arguments = new List<string> { "--allow-anonymous", "--topic", "devices/#;retain" };
if (which == "no-retain")
{
    arguments = ["--allow-anonymous"];
}

if (which == "refused")
{
    arguments = ["--topic", "devices/#;retain"];
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string broker = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The run: each device publishes with curl-mcp's curl tool, as curl -d <state> mqtt://host/<topic>.
// The lamp reports twice, and the second report is the one that counts.
var run = new List<(string Topic, string State)>
{
    ("devices/lamp/state", "on"),
    ("devices/lamp/state", "off"),
    ("devices/door/state", "closed"),
    ("devices/heater/state", which == "wrong" ? "on" : "off"),
};
if (which == "outside")
{
    run.Add(("lab/heater/state", "on"));
}

long lastSequence = 0;
foreach ((string topic, string state) in run)
{
    CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-d", state, broker + topic },
    });
    JsonElement answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    if (which == "refused")
    {
        Console.WriteLine($"publish {topic}: exitCode {answer.GetProperty("exitCode")}");
    }

    // A QoS 0 publish is not acknowledged, so curl can return before the broker has kept it. The broker
    // records the end of each device's connection, after what it sent is applied: wait for that, not a sleep.
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["after_sequence"] = lastSequence, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
    });
    JsonElement found = waited.StructuredContent!.Value;
    JsonElement ended = found.TryGetProperty("event", out JsonElement inner) ? inner : found;
    if (!ended.TryGetProperty("sequence", out JsonElement sequence))
    {
        throw new TimeoutException($"the broker did not end the connection for {topic} in 10 s");
    }

    lastSequence = sequence.GetInt64();
}

// The check: what each topic should hold, against what the broker kept.
var expected = new Dictionary<string, string>
{
    ["devices/lamp/state"] = "off",
    ["devices/door/state"] = "closed",
    ["devices/heater/state"] = "off",
};
if (which == "silent")
{
    expected["devices/pump/state"] = "off";
}

if (which == "outside")
{
    expected["lab/heater/state"] = "off";
}

int bad = 0;
foreach ((string topic, string want) in expected)
{
    CallToolResult read = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
    {
        ["server_id"] = serverId,
        ["topic"] = topic,
    });
    JsonElement kept = read.StructuredContent!.Value;
    if (kept.TryGetProperty("retained", out JsonElement retained) && !retained.GetBoolean())
    {
        Console.WriteLine($"{topic}: nothing retained, expected {want}");
        bad++;
    }
    else if (kept.GetProperty("text").GetString() != want)
    {
        Console.WriteLine($"{topic}: {kept.GetProperty("text").GetString()}, expected {want}");
        bad++;
    }
    else
    {
        Console.WriteLine($"{topic}: {want}");
    }
}

Console.WriteLine(bad == 0 ? "every device left the state it should" : $"{bad} of {expected.Count} topics are wrong");
return bad == 0 ? 0 : 1;
