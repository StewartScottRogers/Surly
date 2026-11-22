// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent has surl-mcp's MQTT 5 broker refuse publishes and subscribes by topic grant and reads the reason codes from
// the curl tool's answers, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   solution       ann publishes 21, bob reads it (the default)
//   outside        ann subscribes outside every grant
//   pub-only       dan, granted pub, subscribes
//   no-grant       cy, with no grant, subscribes
//   sub-only       bob, granted sub, publishes: dropped
//   no-grant-pub   cy publishes: dropped
//   wrong-password a wrong password
//   no-login       no login at all
//   clear          a right password, with no --allow-plaintext-auth
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";
const string Topic = "plant/line1/temp";

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

// The broker: four accounts and one grant line that gives each its right. --mqtt5-disconnect-after-retained ends a
// subscription once the retained messages are sent, so the curl tool returns instead of waiting.
var arguments = new List<string>
{
    "--user", "ann:annpw", "--user", "bob:bobpw", "--user", "cy:cypw", "--user", "dan:danpw",
    "--topic-access", "plant/#=ann:pubsub,bob:sub,dan:pub",
    "--topic", "plant/#;retain",
    "--mqtt5-disconnect-after-retained",
};
if (which != "clear")
{
    arguments.Add("--allow-plaintext-auth");
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
    case "outside":
        await Read("ann:annpw", "office/desk");
        break;
    case "pub-only":
        await Read("dan:danpw", Topic);
        break;
    case "no-grant":
        await Read("cy:cypw", Topic);
        break;
    case "sub-only":
        await Publish("ann:annpw", "21");
        await Publish("bob:bobpw", "99");
        await Read("ann:annpw", Topic);
        Console.WriteLine($"kept: {await Kept()}");
        break;
    case "no-grant-pub":
        await Publish("ann:annpw", "21");
        await Publish("cy:cypw", "77");
        await Read("ann:annpw", Topic);
        Console.WriteLine($"kept: {await Kept()}");
        break;
    case "wrong-password":
        await Read("ann:wrong", Topic);
        break;
    case "no-login":
        await Read(null, Topic);
        break;
    case "clear":
        await Read("ann:annpw", Topic);
        break;
    default:
        await Publish("ann:annpw", "21");
        await Read("bob:bobpw", Topic);
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

// A publish at quality of service 0 is not answered, so curl can return before the broker has applied it or dropped it.
// The broker records the end of the client's connection after, and that is what the agent waits for, with a bound.
async Task Publish(string login, string text)
{
    await Curl(login, "-d", text, broker + Topic);
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

// A subscription: the retained message, the payload alone. A refusal is an exit code and a message, not a tool error.
async Task Read(string? login, string topic)
{
    string who = login?.Split(':')[0] ?? "anonymous";
    JsonElement answer = await Curl(login, broker + topic);
    string text = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()?.TrimEnd() ?? "";
    Console.WriteLine(answer.GetProperty("exitCode").GetInt32() != 0
        ? $"{who} refused: exit {answer.GetProperty("exitCode")}, {error}"
        : $"{who} reads {(text.Length == 0 ? "nothing" : text)}");
}

// What the broker holds for the topic: read_retained_message reads the store without a client, so without a grant.
async Task<string> Kept()
{
    CallToolResult result = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = Topic,
    });
    JsonElement kept = result.StructuredContent!.Value;
    return kept.TryGetProperty("retained", out JsonElement retained) && !retained.GetBoolean() ? "nothing" : kept.GetProperty("text").GetString()!;
}
