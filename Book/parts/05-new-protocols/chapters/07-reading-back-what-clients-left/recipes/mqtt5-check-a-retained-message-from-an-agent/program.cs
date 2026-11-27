// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent has a device publish its last state to surl-mcp's MQTT 5 broker, then reads the retained message back
// with its QoS, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   device     the device publishes on, then off; the agent reads the retained message (the default)
//   seeded     the agent seeds a retained message itself, and a client's publish outside the filter is not kept
//   clear      an empty message clears the topic
//   binary     a payload that is not text
//   nothing    a topic never published to, and a topic outside the filter
//   qos        a topic filter with a QoS of 1
//   closed     a broker with no --allow-anonymous
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "device";

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

// An MQTT 5 broker that keeps what is published under devices/ and ends a subscription once it has sent what it keeps.
var arguments = new List<string> { "--topic", which == "qos" ? "devices/#;retain;qos=1" : "devices/#;retain", "--mqtt5-disconnect-after-retained" };
if (which != "closed")
{
    arguments.Add("--allow-anonymous");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt5://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string broker = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
long lastSequence = 0;

switch (which)
{
    case "seeded":
        // The agent's own publish sets RETAIN, so the broker keeps it with no filter to name the topic.
        await Seed("lab/lamp/state", "on");
        await Read("lab/lamp/state");
        await Curl("-d", "off", broker + "lab/pump/state");
        await Read("lab/pump/state");
        break;
    case "clear":
        await Curl("-d", "on", broker + "devices/lamp/state");
        await Curl("-d", "", broker + "devices/lamp/state");
        await Read("devices/lamp/state");
        break;
    case "binary":
        await Seed("devices/lamp/state", payloadBase64: "AP8B");
        await Read("devices/lamp/state");
        break;
    case "nothing":
        await Read("devices/lamp/state");
        await Curl("-d", "on", broker + "lab/lamp/state");
        await Read("lab/lamp/state");
        break;
    case "closed":
        await Curl("-d", "on", broker + "devices/lamp/state");
        await Read("devices/lamp/state");
        break;
    default:
        // The device: curl publishes at QoS 0, twice; the broker keeps the last.
        await Curl("-d", "on", broker + "devices/lamp/state");
        await Curl("-d", "off", broker + "devices/lamp/state");
        await Read("devices/lamp/state");
        // Reading does not drop it: a second read answers the same.
        await Read("devices/lamp/state");
        // And a client that subscribes later is sent the same message.
        await Curl(broker + "devices/lamp/state");
        break;
}

return 0;

// publish_message publishes as a client would, with no client and no login; retain keeps it as the topic's message.
async Task Seed(string topic, string? text = null, string? payloadBase64 = null)
{
    var publish = new Dictionary<string, object?> { ["server_id"] = server, ["topic"] = topic, ["retain"] = true };
    if (text is not null)
    {
        publish["payload"] = text;
    }
    else
    {
        publish["payload_base64"] = payloadBase64;
    }

    await surl.CallToolAsync("publish_message", publish);
}

// read_retained_message answers topic, qos and text (or base64 when the payload is not text), or retained false, which is not an error.
async Task Read(string topic)
{
    CallToolResult result = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic,
    });
    JsonElement kept = result.StructuredContent!.Value;
    if (kept.TryGetProperty("retained", out JsonElement retained) && !retained.GetBoolean())
    {
        Console.WriteLine($"{topic}: nothing retained");
        return;
    }

    string payload = kept.TryGetProperty("text", out JsonElement text) ? $"text {text}" : $"base64 {kept.GetProperty("base64")}";
    Console.WriteLine($"{topic}: {payload}, qos {kept.GetProperty("qos")}");
}

// The curl tool runs curl's own arguments; -s keeps the progress meter out and -S puts an error message back.
async Task Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(curlArguments).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    string sent = curlArguments[0] == "-d" ? (curlArguments[1] == "" ? "an empty message" : curlArguments[1]) : "";
    string what = curlArguments[0] == "-d" ? $"publish {sent}" : "subscribe";
    Console.WriteLine($"curl {what}: exit {answer.GetProperty("exitCode")}{(output == "" ? "" : $", {output}")}{(error == "" ? "" : $" ({error})")}");

    // A QoS 0 publish is not acknowledged, so curl can return before the broker has kept it. The broker
    // records the end of each client's connection, after what it sent is applied: wait for that, not a sleep.
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_sequence"] = lastSequence, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
    });
    JsonElement found = waited.StructuredContent!.Value;
    JsonElement ended = found.TryGetProperty("event", out JsonElement inner) ? inner : found;
    if (!ended.TryGetProperty("sequence", out JsonElement sequence))
    {
        throw new TimeoutException("the broker did not end curl's connection in 10 s");
    }

    lastSequence = sequence.GetInt64();
}
