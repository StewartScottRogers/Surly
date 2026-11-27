// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds retained readings on several topics of surl-mcp's MQTT broker and works out, from subscriptions
// made with the curl tool, which topics each filter receives and which it misses, over the official MCP SDK.
// Run: dotnet run program.cs -- [filter] [raw]
//   (no argument)  sensors/+/temp
//   <filter>       any filter, written as a subscriber writes it: sensors/#, sensors/kitchen/+, ...
//   <filter> raw   the same, with a # left as it is in the URL, not written %23
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string filter = args.Length > 0 ? args[0] : "sensors/+/temp";
bool raw = args.Length > 1 && args[1] == "raw";

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

// The broker: no login needed.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string broker = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Six readings, seeded as retained: publish_message with retain true sets the flag itself, so no --topic is needed.
var seeded = new Dictionary<string, string>
{
    ["sensors/kitchen/temp"] = "21",
    ["sensors/hall/temp"] = "19",
    ["sensors/kitchen/humidity"] = "40",
    ["sensors/kitchen/oven/temp"] = "180",
    ["sensors/temp"] = "5",
    ["sensors"] = "up",
};
foreach ((string topic, string text) in seeded)
{
    await surl.CallToolAsync("publish_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic, ["payload"] = text, ["retain"] = true,
    });
}

// The subscriber is the curl tool. A # is written %23, since a plain one would be taken as the start of a URL fragment.
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-S", broker + (raw ? filter : filter.Replace("#", "%23")) },
});
JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
int exitCode = answer.GetProperty("exitCode").GetInt32();
if (exitCode != 0)
{
    Console.WriteLine($"{filter}: curl exit {exitCode}: {answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd()}");
    return exitCode;
}

// What came back: each message as the topic's length in two bytes, the topic and the payload, which runs to the
// next message's zero byte, since the topics here are shorter than 256 bytes.
string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
var received = new Dictionary<string, string>();
for (int at = 0; at < output.Length;)
{
    int length = (output[at] << 8) | output[at + 1];
    string topic = output.Substring(at + 2, length);
    int start = at + 2 + length;
    int end = output.IndexOf('\0', start);
    end = end < 0 ? output.Length : end;
    received[topic] = output[start..end];
    at = end;
}

Console.WriteLine($"{filter} receives: {Join(received.Keys)}");
Console.WriteLine($"{filter} misses: {Join(seeded.Keys.Except(received.Keys))}");
return 0;

string Join(IEnumerable<string> topics)
{
    string[] sorted = topics.Order(StringComparer.Ordinal).Select(topic => $"{topic}={seeded[topic]}").ToArray();
    return sorted.Length == 0 ? "nothing" : string.Join(", ", sorted);
}
