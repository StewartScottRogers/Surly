// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks what an app published: surl-mcp runs an MQTT broker that keeps the last reading on a topic,
// curl-mcp's curl tool stands in for the app and publishes, wait_for_event waits for the broker to apply the
// publish, and read_retained_message reads the reading the broker kept. Over the official MCP SDK.
// Run: dotnet run program.cs -- <case>
//   solution       the app publishes 21.5, the check expects 21.5 (the default)
//   wrong          the check expects 22.0
//   twice          the check runs twice: reading does not use the reading up
//   replaced       the app publishes 21.5 and then 22.0; the check expects 22.0
//   earlier        the same, and the check expects 21.5
//   nothing        the app publishes nothing
//   misspelt       the app publishes to sensors/hall/temprature; the check looks at sensors/hall/temp
//   misspelt-kept  the same, and the check looks at the misspelling
//   outside        the app publishes outside the declared filter
//   no-filter      a broker with no --topic
//   closed         a broker with no --allow-anonymous
#:package ModelContextProtocol.Core@2.4.0

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
    case "nothing":
        return Check("sensors/hall/temp", "19.0", await Kept("sensors/hall/temp"));
    case "misspelt":
    case "misspelt-kept":
        await App("sensors/hall/temprature", "19.0");
        return which == "misspelt"
            ? Check("sensors/hall/temp", "19.0", await Kept("sensors/hall/temp"))
            : Check("sensors/hall/temprature", "19.0", await Kept("sensors/hall/temprature"));
    case "outside":
        await App("lab/kitchen/temp", "21.5");
        return Check("lab/kitchen/temp", "21.5", await Kept("lab/kitchen/temp"));
    case "replaced":
    case "earlier":
        await App("sensors/kitchen/temp", "21.5");
        await App("sensors/kitchen/temp", "22.0");
        return Check("sensors/kitchen/temp", which == "replaced" ? "22.0" : "21.5", await Kept("sensors/kitchen/temp"));
    case "closed":
        // The app is refused at its connection: no exchange to wait for, and nothing to read.
        JsonElement refused = await Curl("-d", "21.5", broker + "sensors/kitchen/temp");
        Console.WriteLine($"app refused: exit {refused.GetProperty("exitCode")}, {refused.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd()}");
        return 0;
    default:
        // The app under test: one publish, as the app's own client would send it.
        await App("sensors/kitchen/temp", "21.5");
        string expected = which == "wrong" ? "22.0" : "21.5";
        int result = Check("sensors/kitchen/temp", expected, await Kept("sensors/kitchen/temp"));
        if (which == "twice")
        {
            result |= Check("sensors/kitchen/temp", expected, await Kept("sensors/kitchen/temp"));
        }

        return result;
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

// The app: curl publishes with the retain flag clear. A publish at QoS 0 gets no answer, so the tool can return
// before the broker has applied it; the agent waits for the broker to record the end of that connection, with a
// bound that only a failure reaches, and never sleeps.
async Task App(string topic, string reading)
{
    await Curl("-d", reading, broker + topic);
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_sequence"] = lastSequence, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
    });
    JsonElement found = waited.StructuredContent!.Value;
    JsonElement ended = found.TryGetProperty("event", out JsonElement inner) ? inner : found;
    if (!ended.TryGetProperty("sequence", out JsonElement sequence))
    {
        throw new TimeoutException("the broker did not end the app's connection in 10 s");
    }

    lastSequence = sequence.GetInt64();
}

// What the broker holds for a topic: read_retained_message answers topic, qos and text, or retained false, which is
// not an error. It reads the store with no client and no login; null means nothing is kept.
async Task<string?> Kept(string topic)
{
    CallToolResult result = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic,
    });
    JsonElement kept = result.StructuredContent!.Value;
    return kept.TryGetProperty("retained", out JsonElement retained) && !retained.GetBoolean()
        ? null
        : $"{kept.GetProperty("text").GetString()} at QoS {kept.GetProperty("qos")}";
}

// The agent's assertion: 0 when the broker holds the expected reading, 1 with a message that names what it holds.
static int Check(string topic, string expected, string? kept)
{
    if (kept is null)
    {
        Console.WriteLine($"FAIL: nothing kept on {topic}");
        return 1;
    }

    if (kept != $"{expected} at QoS 0")
    {
        Console.WriteLine($"FAIL: {topic} is {kept}, expected {expected}");
        return 1;
    }

    Console.WriteLine($"ok: {topic} is {kept}");
    return 0;
}
