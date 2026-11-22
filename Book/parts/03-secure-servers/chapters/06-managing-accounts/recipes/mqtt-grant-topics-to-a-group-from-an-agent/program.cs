// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent grants MQTT topics to groups of accounts on surl-mcp's broker and checks, with the curl tool, that a member
// of the wrong group is refused, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   solution        a sensor publishes, a dashboard reads it (the default)
//   second          another member of the sensors group publishes
//   dashboard       a dashboard publishes: dropped; a sensor subscribes: refused
//   outsider        an account in neither group publishes and subscribes
//   grown           the outsider joins the sensors group, and publishes
//   wrong-password  a wrong password
//   no-plaintext    a right password, with no --allow-plaintext-auth
//   bad-grant       three grants that name no account or group
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

if (which == "bad-grant")
{
    await BadGrant("--group", "sensors=sensor1", "--topic-access", "plant/#=sensors:pub");
    await BadGrant("--group", "sensors=sensor1", "--topic-access", "plant/#=@robots:pub");
    await BadGrant("--group", "sensors=sensor1,ghost");
    return 0;
}

// The broker: four accounts, two groups, and one grant line that gives each group its right. sensor3 is in no group,
// until the "grown" case puts it in the sensors group and changes nothing else.
var arguments = new List<string>
{
    "--user", "sensor1:s1pass", "--user", "sensor2:s2pass", "--user", "sensor3:s3pass", "--user", "dash1:d1pass",
    "--group", which == "grown" ? "sensors=sensor1,sensor2,sensor3" : "sensors=sensor1,sensor2",
    "--group", "dashboards=dash1",
    "--topic", "plant/#;retain",
    "--topic-access", "plant/#=@sensors:pub,@dashboards:sub",
};
if (which != "no-plaintext")
{
    arguments.Add("--allow-plaintext-auth");
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
    case "second":
        await Publish("sensor1", "s1pass", "21");
        await Publish("sensor2", "s2pass", "22");
        await Read("dash1", "d1pass");
        break;
    case "dashboard":
        await Publish("sensor1", "s1pass", "22");
        await Publish("dash1", "d1pass", "99");
        Console.WriteLine($"kept: {await Kept()}");
        await Read("sensor1", "s1pass");
        break;
    case "outsider":
        await Publish("sensor1", "s1pass", "22");
        await Publish("sensor3", "s3pass", "77");
        Console.WriteLine($"kept: {await Kept()}");
        await Read("sensor3", "s3pass");
        break;
    case "grown":
        await Publish("sensor3", "s3pass", "23");
        await Read("dash1", "d1pass");
        break;
    case "wrong-password":
        await Read("dash1", "nope");
        break;
    case "no-plaintext":
        await Read("sensor1", "s1pass");
        break;
    default:
        await Publish("sensor1", "s1pass", "21");
        await Read("dash1", "d1pass");
        break;
}

return 0;

// The curl tool runs curl's own arguments: -s keeps the progress meter out, -S puts an error message back, and -u is the login.
async Task<JsonElement> Curl(string login, params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", login }.Concat(curlArguments).ToArray(),
    });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// A publish is not acknowledged, so curl can return before the broker has applied it, or dropped it. The broker records
// the end of the client's connection after, and that is what the agent waits for, with a bound, not a sleep.
async Task Publish(string user, string password, string text)
{
    await Curl($"{user}:{password}", "-d", text, broker + Topic);
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

// A subscription: what comes back is the topic's two-byte length, the topic and the message. A refusal is an exit code, not a tool error.
async Task Read(string user, string password)
{
    JsonElement answer = await Curl($"{user}:{password}", broker + Topic);
    string text = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    Console.WriteLine(answer.GetProperty("exitCode").GetInt32() != 0
        ? $"{user} refused: exit {answer.GetProperty("exitCode")}, {error}"
        : $"{user} reads {(text.Length == 0 ? "nothing" : text[(2 + Topic.Length)..])}");
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

// Grants and groups are checked as the broker starts, so a bad one is a tool error carrying surl's exit code and message.
async Task BadGrant(params string[] options)
{
    CallToolResult failed = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
        ["arguments"] = new[] { "--user", "sensor1:s1pass" }.Concat(options).ToArray(),
    });
    JsonElement detail = failed.StructuredContent!.Value;
    Console.WriteLine($"tool error: {(failed.IsError == true ? "yes" : "no")}, exit_code {detail.GetProperty("exit_code")} ({detail.GetProperty("exit_code_name")})");
    Console.WriteLine(failed.Content.OfType<TextContentBlock>().Single().Text);
}
