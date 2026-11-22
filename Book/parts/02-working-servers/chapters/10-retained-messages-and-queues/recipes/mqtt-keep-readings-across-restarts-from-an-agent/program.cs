// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests that retained readings survive a restart: surl-mcp runs an MQTT broker whose --directory keeps them
// on disk, curl-mcp's curl tool publishes and subscribes, stop_server and a second start_server on the same folder
// restart it, and read_retained_message reads the same reading. Over the official MCP SDK.
// Run: dotnet run program.cs -- <case>
//   restart       publish two readings, restart, a subscriber reads the kitchen's (the default)
//   every-topic   the restart, then a subscription to sensors/# reads both
//   on-disk       the folder's files, while the broker runs and after it stops
//   outside       a publish outside the declared filter is not there after the restart
//   seeded        a reading seeded with publish_message is there after the restart
//   twice         a new reading after the restart, a second restart, and the subscription
//   memory-holds  the same broker with no --directory, before the restart
//   memory        the same, after the restart
//   in-use        a second broker on the folder while the first runs
//   missing       a --directory that does not exist
//   closed        a broker with no --allow-anonymous
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "restart";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The retained messages go in a
// folder of their own under it, made for this case and removed at the end.
string here = Directory.GetCurrentDirectory();
string state = Path.Combine(here, "state-" + which);
Directory.CreateDirectory(state);
string retainedFile = Path.Combine(state, ".surl", "bus", "retained");
string lockFile = Path.Combine(state, ".surl", "lock");

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

bool keep = !which.StartsWith("memory");
string server = "";
string broker = "";
long lastSequence = 0;

try
{
    await Begin(keep ? state : null, which != "closed");
    switch (which)
    {
        case "missing":
            CallToolResult lost = await Start(Path.Combine(state, "nosuch"), true);
            JsonElement lostAnswer = lost.StructuredContent!.Value;
            Console.WriteLine($"exit {lostAnswer.GetProperty("exit_code")}, could not open: {lostAnswer.GetProperty("lines")[0].GetString()!.Contains("Could not open directory")}");
            return 0;
        case "closed":
            // The client is refused at its connection: no exchange to wait for, and nothing to read.
            JsonElement refused = await Curl("-d", "21.5", broker + "sensors/kitchen/temp");
            Console.WriteLine($"client refused: exit {refused.GetProperty("exitCode")}, {refused.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd()}");
            return 0;
        case "seeded":
            // A seed is not a client's publish: retain true sets the flag itself, and the broker writes it to disk.
            await surl.CallToolAsync("publish_message", new Dictionary<string, object?>
            {
                ["server_id"] = server, ["topic"] = "sensors/kitchen/temp", ["payload"] = "21.5", ["retain"] = true,
            });
            break;
        case "outside":
            await Publish("lab/kitchen/temp", "22.0");
            await Publish("sensors/kitchen/temp", "21.5");
            break;
        default:
            await Publish("sensors/kitchen/temp", "21.5");
            await Publish("sensors/hall/temp", "19.0");
            break;
    }

    if (which == "memory-holds")
    {
        await Subscribe("sensors/kitchen/temp");
        return 0;
    }

    if (which == "in-use")
    {
        CallToolResult second = await Start(state, true);
        JsonElement secondAnswer = second.StructuredContent!.Value;
        Console.WriteLine(second.IsError == true
            ? $"second broker: exit {secondAnswer.GetProperty("exit_code")}, in use: {secondAnswer.GetProperty("lines")[0].GetString()!.Contains("is in use by another surl process")}"
            : "second broker: started");
        return 0;
    }

    if (which == "on-disk")
    {
        Console.WriteLine($"running: lock {File.Exists(lockFile)}, retained {File.Exists(retainedFile)}");
        await Stop();
        Console.WriteLine($"stopped: lock {File.Exists(lockFile)}, retained {File.Exists(retainedFile)}");
        return 0;
    }

    // The restart: stop the broker, start another on the same folder, with the same options.
    await Restart();

    switch (which)
    {
        case "every-topic":
            await SubscribeAll();
            break;
        case "outside":
            Console.WriteLine($"lab/kitchen/temp, kept: {await Kept("lab/kitchen/temp")}");
            await Subscribe("lab/kitchen/temp");
            Console.WriteLine($"sensors/kitchen/temp, kept: {await Kept("sensors/kitchen/temp")}");
            break;
        case "twice":
            // The restarted broker keeps what is published to it: replace the hall's reading, restart again.
            await Publish("sensors/hall/temp", "20.0");
            await Restart();
            await SubscribeAll();
            break;
        case "seeded":
            Console.WriteLine($"sensors/kitchen/temp, kept: {await Kept("sensors/kitchen/temp")}");
            await Subscribe("sensors/kitchen/temp");
            break;
        default:
            await Subscribe("sensors/kitchen/temp");
            Console.WriteLine($"kept: {await Kept("sensors/kitchen/temp")}");
            break;
    }

    return 0;
}
finally
{
    // The folder is released before the program deletes it.
    await Stop();
    Directory.Delete(state, true);
}

// The same options every time; the folder is what is kept.
async Task<CallToolResult> Start(string? folder, bool anonymous)
{
    var arguments = new List<string> { "--topic", "sensors/#;retain" };
    if (anonymous)
    {
        arguments.Add("--allow-anonymous");
    }

    if (folder is not null)
    {
        arguments.AddRange(["--directory", folder]);
    }

    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" }, ["arguments"] = arguments,
    });
    return started;
}

// The broker the program works with: its id, for the tools that name a server, and the URL it bound.
async Task Begin(string? folder, bool anonymous)
{
    JsonElement started = (await Start(folder, anonymous)).StructuredContent!.Value;
    server = started.GetProperty("server_id").GetString()!;
    broker = started.GetProperty("bound_urls")[0].GetString()!;
}

// stop_server stops the broker, which releases the folder.
async Task Stop()
{
    if (server != "")
    {
        await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = server });
        server = "";
    }
}

// A restart: the old broker is stopped, and the new one is a new server with its own id, port and record of events,
// so the sequence the agent waits after starts again at zero.
async Task Restart()
{
    await Stop();
    lastSequence = 0;
    await Begin(keep ? state : null, true);
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

// The app's publish: curl sends it with the retain flag clear. It gets no answer at QoS 0, so the agent waits for the
// broker to record the end of curl's connection, with a bound that only a failure reaches, before it stops the broker.
async Task Publish(string topic, string reading)
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
        throw new TimeoutException("the broker did not end curl's connection in 10 s");
    }

    lastSequence = sequence.GetInt64();
}

// A subscriber: curl subscribes, and what comes back is each retained message as the topic's two-byte length, the
// topic and the text; the call returns by itself once they are sent.
async Task<string> Read(string path)
{
    JsonElement answer = await Curl(broker + path);
    return answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
}

async Task Subscribe(string topic)
{
    string text = await Read(topic);
    Console.WriteLine($"{topic}: subscriber reads {(text.Length == 0 ? "nothing" : text[(2 + topic.Length)..])}");
}

// A subscription to sensors/#, which is sensors/%23 in a URL: every kept topic, one after the other.
async Task SubscribeAll()
{
    string text = await Read("sensors/%23");
    Console.WriteLine($"sensors/#: {text.Length} bytes, hall {text.Contains("\u0011sensors/hall/temp" + (which == "twice" ? "20.0" : "19.0"))}, kitchen {text.Contains("\u0014sensors/kitchen/temp21.5")}");
}

// What the broker holds for a topic: read_retained_message answers topic, qos and text, or retained false, which is
// not an error. It reads the store with no client.
async Task<string> Kept(string topic)
{
    CallToolResult result = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic,
    });
    JsonElement kept = result.StructuredContent!.Value;
    return kept.TryGetProperty("retained", out JsonElement retained) && !retained.GetBoolean()
        ? "nothing"
        : $"{kept.GetProperty("text").GetString()} at QoS {kept.GetProperty("qos")}";
}
