// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent has surl-mcp's broker serve MQTT 5 over TCP and over WebSocket, publishes over one and reads over the other
// with the curl tool, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   solution    a message published over TCP is read over WebSocket (the default)
//   to-tcp      a message published over WebSocket is read over TCP
//   later       the same message is read twice, by two clients
//   wildcard    two topics are read with one filter
//   outside     a topic outside the filter keeps nothing
//   clear       an empty publish over WebSocket clears a topic
//   plain       a WebSocket client that is not an MQTT client
//   other-path  a WebSocket upgrade to another path
//   no-login    an account broker, a client with no login
//   clear-text  an account broker, the right password over the WebSocket
//   allowed     the same, with --allow-plaintext-auth
//   wrong       the same, with a wrong password
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";
bool accounts = which is "no-login" or "clear-text" or "allowed" or "wrong";

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

// One broker on two listen URLs, one message bus: MQTT 5 over TCP for the devices and over WebSocket for the dashboard.
// --mqtt5-disconnect-after-retained ends a subscription once the retained messages are sent, so the curl tool returns.
var arguments = new List<string> { "--topic", "dash/#;retain", "--mqtt5-disconnect-after-retained" };
arguments.AddRange(accounts ? ["--user", "ann:s3cret"] : ["--allow-anonymous"]);
if (which is "allowed" or "wrong")
{
    arguments.Add("--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt5://127.0.0.1:0", "mqtt5+ws://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string tcp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string ws = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;
long lastSequence = 0;

switch (which)
{
    case "to-tcp":
        await Publish(ws, "dash/humidity", "22");
        await Read("device", tcp, "dash/humidity");
        break;
    case "later":
        await Publish(tcp, "dash/temp", "21");
        await Read("first", ws, "dash/temp");
        await Read("second", ws, "dash/temp");
        break;
    case "wildcard":
        await Publish(tcp, "dash/temp", "21");
        await Publish(tcp, "dash/humidity", "22");
        await Read("dashboard", ws, "dash/%23", sorted: true);
        break;
    case "outside":
        await Publish(tcp, "lab/temp", "5");
        await Read("dashboard", ws, "lab/temp");
        break;
    case "clear":
        await Publish(tcp, "dash/temp", "21");
        await Publish(ws, "dash/temp", "");
        await Read("device", tcp, "dash/temp");
        break;
    case "plain":
        await Upgrade("mqtt-less client", ws.Replace("mqtt5+ws://", "ws://") + "mqtt");
        break;
    case "other-path":
        await Upgrade("other path", ws.Replace("mqtt5+ws://", "ws://") + "other");
        break;
    case "no-login":
        await Read("anonymous", ws, "dash/temp");
        break;
    case "clear-text":
        await Read("ann", ws, "dash/temp", "ann:s3cret");
        break;
    case "allowed":
        await Publish(ws, "dash/temp", "21", "ann:s3cret");
        await Read("ann", ws, "dash/temp", "ann:s3cret");
        break;
    case "wrong":
        await Read("ann", ws, "dash/temp", "ann:wrong");
        break;
    default:
        await Publish(tcp, "dash/temp", "21");
        await Read("dashboard", ws, "dash/temp");
        break;
}

return 0;

// The curl tool runs curl's own arguments: -s keeps the progress meter out, -S puts an error message back, and -u is the login.
async Task<JsonElement> Curl(string? login, params string[] curlArguments)
{
    var all = new List<string> { "-s", "-S" };
    if (login is not null)
    {
        all.AddRange(["-u", login]);
    }

    all.AddRange(curlArguments);
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = all.ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// A publish at quality of service 0 is not answered, so curl can return before the broker has applied it. The broker
// records the end of the client's connection after, and that is what the agent waits for, with a bound, not a sleep.
async Task Publish(string listener, string topic, string text, string? login = null)
{
    await Curl(login, "-d", text, listener + topic);
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

// A subscription: the retained messages, the payloads alone. A refusal is an exit code and a message, not a tool error.
async Task Read(string who, string listener, string topic, string? login = null, bool sorted = false)
{
    JsonElement answer = await Curl(login, listener + topic);
    string text = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()?.TrimEnd() ?? "";
    if (sorted)
    {
        // Two payloads arrive one after the other, in no promised order: show them in a fixed one.
        text = string.Concat(Enumerable.Range(0, text.Length / 2).Select(i => text.Substring(i * 2, 2)).Order());
    }

    Console.WriteLine(answer.GetProperty("exitCode").GetInt32() != 0
        ? $"{who} refused: exit {answer.GetProperty("exitCode")}, {error}"
        : $"{who} reads {(text.Length == 0 ? "nothing" : text)}");
}

// A WebSocket client that is not an MQTT client: the upgrade is refused, and the refusal is an exit code.
async Task Upgrade(string who, string url)
{
    JsonElement answer = await Curl(null, url);
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()?.TrimEnd() ?? "";
    Console.WriteLine($"{who} refused: exit {answer.GetProperty("exitCode")}, {error}");
}
