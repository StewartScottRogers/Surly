// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts one surl-mcp broker that serves MQTT 3.1.1 and MQTT 5 over one bus, plays an old and a new device
// against it with curl-mcp's curl tool, and reads what the broker kept. Run: dotnet run program.cs -- <case> [version]
//   old-to-new       the old device publishes, the new one subscribes
//   new-to-old       the new device publishes, the old one subscribes
//   replaced         the old device publishes over what the new one left
//   clear            the new device clears the topic, the old one subscribes
//   outside          a publish outside the --topic filter, kept by neither
//   login            one account, the same login on both versions
//   wrong-password   mqtt | mqtt5: a wrong password
//   no-login         mqtt | mqtt5: no user name
//   clear-password   mqtt | mqtt5: the right password, with no --allow-plaintext-auth
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args[0];
string version = args.Length > 1 ? args[1] : "mqtt";
bool accounts = which is "login" or "wrong-password" or "no-login" or "clear-password";

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

// One broker, two listeners: bound_urls[0] is MQTT 3.1.1 and [1] is MQTT 5, on one bus. --topic keeps what a device
// publishes under plant, and --mqtt5-disconnect-after-retained ends an MQTT 5 subscription once it has been sent.
var arguments = new List<string> { "--topic", "plant/#;retain", "--mqtt5-disconnect-after-retained" };
if (accounts)
{
    arguments.AddRange(["--user", "lamp:s3cret"]);
    if (which != "clear-password") arguments.Add("--allow-plaintext-auth");
}
else
{
    arguments.Add("--allow-anonymous");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0", "mqtt5://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string oldUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string newUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;
long lastSequence = 0;
string topic = "plant/hall/temp";

switch (which)
{
    case "old-to-new":
        await Publish("mqtt", oldUrl + topic, "68");
        await Subscribe("mqtt5", newUrl + topic);
        await Read(topic);
        break;
    case "new-to-old":
        await Publish("mqtt5", newUrl + topic, "71");
        await Subscribe("mqtt", oldUrl + topic);
        break;
    case "replaced":
        await Publish("mqtt5", newUrl + topic, "71");
        await Publish("mqtt", oldUrl + topic, "72");
        await Subscribe("mqtt5", newUrl + topic);
        break;
    case "clear":
        await Publish("mqtt", oldUrl + topic, "71");
        await Publish("mqtt5", newUrl + topic, "");
        await Subscribe("mqtt", oldUrl + topic);
        await Read(topic);
        break;
    case "outside":
        await Publish("mqtt5", newUrl + "garden/temp", "5");
        await Subscribe("mqtt", oldUrl + "garden/temp");
        await Read("garden/temp");
        break;
    case "login":
        await Publish("mqtt", oldUrl + "plant/lamp/temp", "21", "lamp:s3cret");
        await Subscribe("mqtt5", newUrl + "plant/lamp/temp", "lamp:s3cret");
        break;
    default:
        string login = which == "no-login" ? "" : which == "wrong-password" ? "lamp:wrong" : "lamp:s3cret";
        await Publish(version, (version == "mqtt5" ? newUrl : oldUrl) + "plant/lamp/temp", "1", login);
        await Read("plant/lamp/temp");
        break;
}

return 0;

// read_retained_message answers topic, qos and text, or retained false, which is a result and not an error.
async Task Read(string path)
{
    CallToolResult result = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = path,
    });
    JsonElement kept = result.StructuredContent!.Value;
    Console.WriteLine(kept.TryGetProperty("text", out JsonElement text)
        ? $"{path}: text {text.GetString()}, qos {kept.GetProperty("qos")}"
        : $"{path}: nothing retained");
}

async Task Publish(string label, string url, string message, string login = "")
{
    JsonElement answer = await Curl(login == "" ? ["-d", message, url] : ["-u", login, "-d", message, url]);
    string sent = message == "" ? "an empty message" : message;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    Console.WriteLine($"curl {label} publish {sent}: exit {answer.GetProperty("exitCode")}{(error == "" ? "" : $" ({error})")}");
}

// An MQTT 5 subscriber is written the payload alone; an MQTT 3.1.1 one the topic's length in two bytes, the topic, then the payload.
async Task Subscribe(string label, string url, string login = "")
{
    JsonElement answer = await Curl(login == "" ? [url] : ["-u", login, url]);
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string seen;
    if (output == "")
    {
        seen = "nothing";
    }
    else if (label == "mqtt")
    {
        int length = (output[0] << 8) | output[1];
        seen = $"length {length}, topic {output.Substring(2, length)}, payload {output[(2 + length)..]}";
    }
    else
    {
        seen = output;
    }

    Console.WriteLine($"curl {label} subscribe: exit {answer.GetProperty("exitCode")}, {seen}");
}

// The curl tool runs curl's own arguments; -s keeps the progress meter out and -S puts an error message back.
async Task<JsonElement> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(curlArguments).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();

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
    return answer;
}
