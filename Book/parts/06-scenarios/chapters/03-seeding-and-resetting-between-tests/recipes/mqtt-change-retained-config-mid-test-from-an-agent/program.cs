// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds a device's retained config through surl-mcp, changes it mid-test, and reads it as the device would
// through curl-mcp, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   change     seed, device reads, change, device reads again (the default)
//   twice      two changes in a row, then a device connects
//   topics     a change to one topic leaves another's
//   clear      an empty message clears the config
//   filter     a topic outside --topic, with a curl publish and with a seeded one
//   no-retain  a broker with no ;retain at all
//   restart    a broker with a data directory, stopped and started again
//   closed     a broker with no --allow-anonymous
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "change";

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

// The broker: it keeps what is published under devices/ and lets a device in with no login.
var arguments = new List<string>();
if (which != "closed")
{
    arguments.Add("--allow-anonymous");
}

if (which != "no-retain")
{
    arguments.AddRange(["--topic", "devices/#;retain"]);
}

if (which == "restart")
{
    // A directory on disk keeps the retained messages: state/ is in the folder the host is confined to.
    arguments.AddRange(["--directory", "state"]);
}

(string server, string broker) = await Start();

switch (which)
{
    case "twice":
        await Seed("devices/door/config", "interval=30");
        await Seed("devices/door/config", "interval=5");
        await Device("devices/door/config");
        break;
    case "topics":
        await Seed("devices/lamp/config", "interval=15");
        await Seed("devices/door/config", "interval=5");
        await Seed("devices/door/config", "interval=10");
        await Device("devices/lamp/config");
        await Device("devices/door/config");
        break;
    case "clear":
        await Seed("devices/lamp/config", "interval=60");
        await Seed("devices/lamp/config", "");
        await Device("devices/lamp/config");
        Console.WriteLine($"kept: {await Kept("devices/lamp/config")}");
        break;
    case "filter":
    case "no-retain":
        // A client's publish has the retain flag clear, so only the filter keeps it; a seed sets the flag itself.
        string topic = which == "filter" ? "lab/lamp/config" : "devices/lamp/config";
        await Publish(topic, "interval=60");
        Console.WriteLine($"curl publish, kept: {await Kept(topic)}");
        await Seed(topic, "interval=60");
        Console.WriteLine($"seeded, kept: {await Kept(topic)}");
        break;
    case "restart":
        await Seed("devices/lamp/config", "interval=60");
        await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = server });
        (server, broker) = await Start();
        Console.WriteLine($"after the restart: {await Kept("devices/lamp/config")}");
        break;
    case "closed":
        await Seed("devices/lamp/config", "interval=60");
        await Device("devices/lamp/config");
        break;
    default:
        // The seed, before the test: the retain flag is set by the call itself.
        await Seed("devices/lamp/config", "interval=60");
        await Device("devices/lamp/config");

        // The change, in the middle of the test: the new message replaces the old on the topic.
        await Seed("devices/lamp/config", "interval=15");
        await Device("devices/lamp/config");
        Console.WriteLine($"kept: {await Kept("devices/lamp/config")}");
        break;
}

return 0;

async Task<(string Server, string Broker)> Start()
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
        ["arguments"] = arguments,
    });
    return (started.StructuredContent!.Value.GetProperty("server_id").GetString()!, started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!);
}

// publish_message publishes as a client would, with no client and no login; retain keeps it as the topic's message.
async Task Seed(string topic, string text)
{
    CallToolResult result = await surl.CallToolAsync("publish_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic, ["payload"] = text, ["retain"] = true,
    });
    if (result.IsError == true)
    {
        Console.WriteLine($"publish_message {topic}: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
    }
}

// The curl tool runs curl's own arguments; -s keeps the progress meter out and -S puts an error message back.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(curlArguments).ToArray(),
    });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// What the device reads: curl subscribes to the topic, and what comes back is the topic's two-byte length, the topic and the message.
async Task Device(string topic)
{
    JsonElement answer = await Curl(broker + topic);
    string text = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    Console.WriteLine(answer.GetProperty("exitCode").GetInt32() != 0
        ? $"{topic}: device refused: exit {answer.GetProperty("exitCode")}, {error}"
        : $"{topic}: device reads {(text.Length == 0 ? "nothing" : text[(2 + topic.Length)..])}");
}

// A publish as the device's own client makes it, with the retain flag clear.
async Task Publish(string topic, string text) => await Curl("-d", text, broker + topic);

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
