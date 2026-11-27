// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reads a topic's retained message from an MQTT broker two ways: from the store, with
// surl-mcp's read_retained_message, and as a subscriber, with curl-mcp's curl tool.
// Run: dotnet run program.cs -- <case>
//   solution       publish_message with retain; read_retained_message and a curl subscribe agree
//   binary         a payload that is not text, as payload_base64
//   large          a payload of 100,000 bytes
//   rooms          one message per room, read with a wildcard
//   replaced       a second publish_message on the topic
//   nothing        a topic with no message
//   cleared        an empty payload takes the message away
//   not-retained   publish_message with retain false
//   curl-publish   curl publishes on the topic: wait for the broker, then read the store
//   closed         a broker with no --allow-anonymous
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
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

// surl-mcp: an MQTT broker on a free port. --allow-anonymous lets a client connect with no user name;
// it is a loosening, and the broker warns of it in log_lines. No --topic filter: nothing needs one.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
    ["arguments"] = which == "closed" ? Array.Empty<string>() : new[] { "--allow-anonymous" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
const string topic = "devices/lamp/state";
long lastSequence = 0;

switch (which)
{
    case "solution":
        await Publish("{\"on\":true}", retain: true);
        Console.WriteLine("store: " + await Read());
        Console.WriteLine("subscriber: " + await Subscribe());
        break;
    case "binary":
        await Publish(Convert.ToBase64String([0x00, 0xFF, 0x10, 0x80]), retain: true, base64: true);
        Console.WriteLine("store: " + await Read());
        Console.WriteLine("subscriber: " + await Subscribe());
        break;
    case "large":
        byte[] big = new byte[100_000];
        new Random(5).NextBytes(big);
        await Publish(Convert.ToBase64String(big), retain: true, base64: true);
        CallToolResult stored = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?> { ["server_id"] = server, ["topic"] = topic });
        Console.WriteLine($"store identical: {Convert.FromBase64String(stored.StructuredContent!.Value.GetProperty("base64").GetString()!).SequenceEqual(big)}");
        var (bigExit, bigOutput, _, bigIsBase64) = await CurlCall("-s", "-S", $"{url}{topic}");
        byte[] received = Convert.FromBase64String(bigOutput)[(2 + topic.Length)..];
        Console.WriteLine($"subscriber: exit {bigExit}, {received.Length} bytes, identical: {received.SequenceEqual(big)}");
        break;
    case "rooms":
        await Publish("21.5", retain: true, to: "home/kitchen/temp");
        await Publish("19.0", retain: true, to: "home/hall/temp");
        await Publish("12.5", retain: true, to: "home/cellar/temp");
        // A # in the URL is a fragment, so the filter is written %23.
        var (_, rooms, _, _) = await CurlCall("-s", "-S", $"{url}home/%23");
        Console.WriteLine($"subscriber: {rooms.Length} bytes");
        foreach (string t in new[] { "home/cellar/temp", "home/hall/temp", "home/kitchen/temp" })
        {
            Console.WriteLine($"{t}: {rooms.Contains(t)}");
        }

        break;
    case "replaced":
        await Publish("on", retain: true);
        await Publish("off", retain: true);
        Console.WriteLine("store: " + await Read());
        Console.WriteLine("subscriber: " + await Subscribe());
        break;
    case "nothing":
        Console.WriteLine("store: " + await Read());
        Console.WriteLine("subscriber: " + await Subscribe());
        break;
    case "cleared":
        await Publish("on", retain: true);
        await Publish("", retain: true);
        Console.WriteLine("store: " + await Read());
        Console.WriteLine("subscriber: " + await Subscribe());
        break;
    case "not-retained":
        await Publish("on", retain: false);
        Console.WriteLine("store: " + await Read());
        Console.WriteLine("subscriber: " + await Subscribe());
        break;
    case "curl-publish":
        await Publish("on", retain: true);
        // A client's publish has the retain flag clear, and QoS 0 is not acknowledged: curl returns before
        // the broker has dealt with it. Wait for the broker's record of the exchange, then read the store.
        Console.WriteLine("curl publish: exit " + await CurlRun("-d", "off", $"{url}{topic}"));
        Console.WriteLine("store: " + await Read());
        break;
    case "closed":
        await Publish("on", retain: true);
        Console.WriteLine("store: " + await Read());
        Console.WriteLine("subscriber: " + await Subscribe());
        break;
}

return 0;

// surl-mcp's publish_message: the call sets RETAIN itself when retain is true, so the message is kept
// with no client, no login and no filter, and is kept by the time the tool answers.
async Task Publish(string payload, bool retain, bool base64 = false, string? to = null)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = server, ["topic"] = to ?? topic, ["retain"] = retain };
    arguments[base64 ? "payload_base64" : "payload"] = payload;
    CallToolResult result = await surl.CallToolAsync("publish_message", arguments);
    if (result.IsError == true)
    {
        throw new InvalidOperationException(string.Join(" ", result.Content.OfType<TextContentBlock>().Select(t => t.Text)));
    }
}

// read_retained_message: the store, with no client and no grants. text for a UTF-8 payload, base64
// for any other, and {"retained": false} (not a tool error) when nothing is kept.
async Task<string> Read()
{
    CallToolResult result = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?> { ["server_id"] = server, ["topic"] = topic });
    JsonElement kept = result.StructuredContent!.Value;
    if (kept.TryGetProperty("retained", out JsonElement retained) && !retained.GetBoolean())
    {
        return "nothing kept";
    }

    string what = kept.TryGetProperty("text", out JsonElement text) ? $"text {text.GetString()}" : $"base64 {kept.GetProperty("base64").GetString()}";
    return $"{what}, QoS {kept.GetProperty("qos").GetInt32()}";
}

// curl-mcp as a subscriber: curl with no -d subscribes, and the broker ends the subscribe itself once it
// has sent what is kept. The answer is the topic's two-byte length, the topic and the payload.
async Task<string> Subscribe()
{
    var (exit, output, error, isBase64) = await CurlCall("-s", "-S", $"{url}{topic}");
    if (exit != 0)
    {
        return $"curl exit {exit}, {error.Trim()}";
    }

    byte[] bytes = isBase64 ? Convert.FromBase64String(output) : Encoding.UTF8.GetBytes(output);
    if (bytes.Length == 0)
    {
        return "nothing sent";
    }

    int prefix = 2 + topic.Length;
    return bytes[0] == 0 && bytes[1] == topic.Length
        ? $"topic {Encoding.ASCII.GetString(bytes, 2, topic.Length)}, payload {(isBase64 ? "hex " + Convert.ToHexString(bytes[prefix..]) : Encoding.UTF8.GetString(bytes[prefix..]))}"
        : "unexpected answer";
}

async Task<int> CurlRun(params string[] arguments)
{
    var (exit, _, _, _) = await CurlCall(["-s", "-S", .. arguments]);
    return exit;
}

// One curl call, then wait for the broker to end the client's connection: the exchange ends after it has
// applied what the client sent. after_sequence moves on with each one.
async Task<(int Exit, string Output, string Error, bool IsBase64)> CurlCall(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement output = answer.GetProperty("standardOutput");
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
    return (answer.GetProperty("exitCode").GetInt32(), output.GetProperty("content").GetString() ?? "",
        answer.GetProperty("standardError").GetProperty("content").GetString() ?? "", output.GetProperty("isBase64").GetBoolean());
}
