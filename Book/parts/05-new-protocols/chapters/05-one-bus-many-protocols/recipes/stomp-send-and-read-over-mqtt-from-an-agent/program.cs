// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent sends over STOMP and reads over MQTT with curl-mcp, against a surl-mcp server that keeps what is sent
// under devices/, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   transfer              send on to devices/lamp/state, read it over MQTT (the default)
//   latest-wins           send on, then off, to one topic
//   empty-clears          send on, then an empty body, to one topic
//   wildcard              send to two topics, read devices/hall/#
//   outside-the-filter    send to a topic no --topic filter matches
//   no-filter             the same send to a server with no --topic filter
//   queue-is-not-a-topic  send to a /queue/ destination
//   stomp-subscriber      subscribe over STOMP to a topic whose message is kept
//   read-the-store        read the kept message with read_retained_message, no client
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "transfer";

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

// STOMP and MQTT on one bus. --topic keeps what is sent under devices/; --idle-timeout ends a STOMP subscriber
// once nothing arrives; --allow-anonymous lets both clients in without an account, for a test.
var arguments = new List<string> { "--allow-anonymous", "--idle-timeout", "1" };
if (which != "no-filter")
{
    arguments.AddRange(["--topic", "devices/#;retain"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "stomp://127.0.0.1:0", "mqtt://127.0.0.1:0" },
    ["arguments"] = arguments.ToArray(),
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string stomp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string mqtt = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;
long lastSequence = 0;
CallToolResult last = started;

switch (which)
{
    case "latest-wins":
        await Send("topic/devices/fan/state", "on");
        await Send("topic/devices/fan/state", "off");
        await Read("devices/fan/state");
        break;
    case "empty-clears":
        await Send("topic/devices/door/state", "on");
        await Send("topic/devices/door/state", "");
        await Read("devices/door/state");
        break;
    case "wildcard":
        await Send("topic/devices/hall/temp", "21.5");
        await Send("topic/devices/hall/humidity", "55");
        await Read("devices/hall/%23");
        break;
    case "outside-the-filter":
        await Send("topic/other/lamp", "on");
        await Read("other/lamp");
        break;
    case "queue-is-not-a-topic":
        await Send("queue/devices/jobs", "job");
        await Read("devices/jobs");
        break;
    case "stomp-subscriber":
        await Send("topic/devices/lamp/state", "on");
        await Read("devices/lamp/state");
        JsonElement subscribed = await Curl("-s", "-m", "10", stomp + "topic/devices/lamp/state");
        await WaitForEnd();
        Console.WriteLine($"STOMP subscriber was sent {subscribed.GetProperty("standardOutput").GetProperty("content").GetString()!.Length} bytes, exit {subscribed.GetProperty("exitCode")}");
        break;
    case "read-the-store":
        await Send("topic/devices/lamp/state", "on");
        last = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["topic"] = "devices/lamp/state",
        });
        Console.WriteLine($"retained text: {last.StructuredContent!.Value.GetProperty("text")}");
        break;
    default:
        await Send("topic/devices/lamp/state", "on");
        await Read("devices/lamp/state");
        break;
}

// The exit code says whether the last call was a tool error.
Console.WriteLine($"tool error: {(last.IsError == true ? "yes" : "no")}");
return last.IsError == true ? 1 : 0;

// The curl tool runs curl's own arguments; the answer is JSON text with exitCode and the two streams.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    last = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    return JsonDocument.Parse(last.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// A STOMP send: -d with the body to the destination. curl can return before the broker has applied it, so the agent
// waits for the broker to record the end of the exchange, with a bound.
async Task Send(string destination, string body)
{
    await Curl("-s", "-d", body, stomp + destination);
    await WaitForEnd();
}

// An MQTT subscribe: each retained message as a two-byte topic length, the topic and the payload; it ends by itself.
async Task Read(string topic)
{
    JsonElement answer = await Curl("-s", mqtt + topic);
    await WaitForEnd();
    JsonElement output = answer.GetProperty("standardOutput");
    byte[] bytes = output.GetProperty("isBase64").GetBoolean()
        ? Convert.FromBase64String(output.GetProperty("content").GetString()!)
        : Encoding.UTF8.GetBytes(output.GetProperty("content").GetString()!);
    var messages = new List<string>();
    for (int at = 0; at < bytes.Length;)
    {
        int length = (bytes[at] << 8) | bytes[at + 1];
        string name = Encoding.UTF8.GetString(bytes, at + 2, length);
        int end = Array.IndexOf(bytes, (byte)0, at + 2 + length);
        end = end < 0 ? bytes.Length : end;
        messages.Add($"{name}: {Encoding.UTF8.GetString(bytes, at + 2 + length, end - at - 2 - length)}");
        at = end;
    }

    foreach (string line in messages.Order())
    {
        Console.WriteLine(line);
    }

    Console.WriteLine($"{messages.Count} retained, {bytes.Length} bytes");
}

async Task WaitForEnd()
{
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_sequence"] = lastSequence, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
    });
    JsonElement found = waited.StructuredContent!.Value;
    JsonElement ended = found.TryGetProperty("event", out JsonElement inner) ? inner : found;
    if (!ended.TryGetProperty("sequence", out JsonElement sequence))
    {
        throw new TimeoutException("the broker did not end the client's connection in 10 s");
    }

    lastSequence = sequence.GetInt64();
}
