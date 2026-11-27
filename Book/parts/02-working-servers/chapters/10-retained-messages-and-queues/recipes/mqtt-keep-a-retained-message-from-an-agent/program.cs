// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent keeps a retained reading on surl-mcp's MQTT broker, by seeding it with publish_message or by a
// client's publish under a declared topic, and reads it as a later subscriber would through curl-mcp, over the
// official MCP SDK. Run: dotnet run program.cs -- <case>
//   seed       seed a reading, a later subscriber reads it twice (the default)
//   filter     a client publishes under the declared filter, and a later subscriber reads it
//   replaced   a second publish to a topic replaces the first
//   topics     each topic keeps its own reading
//   outside    a topic outside the filter: a client's publish is not kept, a seeded one is
//   no-filter  a broker with no --topic: the same
//   clear      an empty message clears the topic
//   closed     a broker with no --allow-anonymous
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "seed";

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

// The broker: it keeps what is published under sensors/ and lets a client in with no login.
var arguments = new List<string>();
if (which != "closed")
{
    arguments.Add("--allow-anonymous");
}

if (which != "no-filter")
{
    arguments.AddRange(["--topic", "sensors/#;retain"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string broker = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
long lastSequence = 0;

switch (which)
{
    case "filter":
        await Publish("sensors/kitchen/temp", "21.5");
        Console.WriteLine($"kept: {await Kept("sensors/kitchen/temp")}");
        await Subscribe("sensors/kitchen/temp");
        break;
    case "replaced":
        await Publish("sensors/kitchen/temp", "21.5");
        await Publish("sensors/kitchen/temp", "22.0");
        await Subscribe("sensors/kitchen/temp");
        break;
    case "topics":
        await Publish("sensors/kitchen/temp", "22.0");
        await Publish("sensors/hall/temp", "19.0");
        await Subscribe("sensors/hall/temp");
        await Subscribe("sensors/kitchen/temp");
        break;
    case "outside":
    case "no-filter":
        string topic = which == "outside" ? "lab/kitchen/temp" : "sensors/kitchen/temp";
        await Publish(topic, "21.5");
        Console.WriteLine($"client publish, kept: {await Kept(topic)}");
        await Seed(topic, "21.5");
        Console.WriteLine($"seeded, kept: {await Kept(topic)}");
        await Subscribe(topic);
        break;
    case "clear":
        await Publish("sensors/hall/temp", "19.0");
        await Publish("sensors/hall/temp", "");
        await Subscribe("sensors/hall/temp");
        Console.WriteLine($"kept: {await Kept("sensors/hall/temp")}");
        break;
    case "closed":
        await Seed("sensors/kitchen/temp", "21.5");
        await Subscribe("sensors/kitchen/temp");
        break;
    default:
        // The seed: retain true sets the flag itself, so no filter is needed to keep it.
        await Seed("sensors/kitchen/temp", "21.5");
        await Subscribe("sensors/kitchen/temp");
        // A second subscriber is sent it too: reading does not use it up.
        await Subscribe("sensors/kitchen/temp");
        Console.WriteLine($"kept: {await Kept("sensors/kitchen/temp")}");
        break;
}

return 0;

// publish_message publishes as a client would, with no client and no login; retain keeps it as the topic's message.
async Task Seed(string topic, string text) =>
    await surl.CallToolAsync("publish_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic, ["payload"] = text, ["retain"] = true,
    });

// The curl tool runs curl's own arguments; -s keeps the progress meter out and -S puts an error message back.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(curlArguments).ToArray(),
    });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// A publish as a client makes it: curl sends it with the retain flag clear. It is not acknowledged, so curl can
// return before the broker has applied it; the broker records the end of the client's connection after, and
// that is what the agent waits for, with a bound, not a sleep.
async Task Publish(string topic, string text)
{
    await Curl("-d", text, broker + topic);
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

// A later subscriber: curl subscribes, and what comes back is the topic's two-byte length, the topic and the message.
async Task Subscribe(string topic)
{
    JsonElement answer = await Curl(broker + topic);
    string text = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    Console.WriteLine(answer.GetProperty("exitCode").GetInt32() != 0
        ? $"{topic}: subscriber refused: exit {answer.GetProperty("exitCode")}, {error}"
        : $"{topic}: subscriber reads {(text.Length == 0 ? "nothing" : text[(2 + topic.Length)..])}");
}

// What the broker holds for a topic: read_retained_message answers topic, qos and text, or retained false, which is not an error.
async Task<string> Kept(string topic)
{
    CallToolResult result = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic,
    });
    JsonElement kept = result.StructuredContent!.Value;
    return kept.TryGetProperty("retained", out JsonElement retained) && !retained.GetBoolean()
        ? "nothing"
        : $"{kept.GetProperty("text")} at QoS {kept.GetProperty("qos")}";
}
