// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a sensor-to-alert flow: surl-mcp is the MQTT broker and the mail server, curl-mcp publishes a
// reading as the sensor would and runs the service's calls, and surl-mcp reports the mail that arrived.
// Run: dotnet run program.cs -- <scenario>
//   solution   the boiler reads 81.5 against a limit of 75
//   quiet      readings at or under the limit, and a sensor that never published
#:package ModelContextProtocol.Core@*

using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "solution";

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

// One server is the broker and the mail server, with POP3 to read the mailbox; --topic keeps the latest message on every topic under sensors/.
JsonElement started = (await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0", "smtp://127.0.0.1:0", "pop3://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous", "--topic", "sensors/#;retain" },
})).StructuredContent!.Value;
string id = started.GetProperty("server_id").GetString()!;
string mqtt = started.GetProperty("bound_urls")[0].GetString()!, smtp = started.GetProperty("bound_urls")[1].GetString()!,
    pop3 = started.GetProperty("bound_urls")[2].GetString()!;
long lastSequence = 0;

if (scenario == "solution")
{
    await Publish("boiler", "81.5");
    Console.WriteLine($"service: {await Service("boiler", 75)}");

    // The check: wait, at most 10 s, for the mail store to report the delivery, then read the mailbox over POP3
    // with the curl tool: message 1 is the alert, and the list has one line per message.
    JsonElement delivered = await Wait("store_changed", "mail");
    Console.WriteLine($"delivered: {delivered.GetProperty("change").GetString()} {delivered.GetProperty("name").GetString()}");
    try
    {
        await Surl("list_messages", new() { ["owner"] = "", ["mailbox"] = "INBOX" });
    }
    catch (McpProtocolException refused)
    {
        Console.WriteLine($"list_messages for the empty owner: {refused.Message}");
    }

    (_, string alert) = await Curl("-s", "-m", "10", $"{pop3}1");
    string[] lines = alert.Split("\r\n");
    Console.WriteLine($"alert: {string.Join(" | ", lines.Where(l => !l.StartsWith("Received:", StringComparison.Ordinal) && l.Length > 0))}");
    (_, string list) = await Curl("-s", "-m", "10", pop3);
    Console.WriteLine($"mailbox: {list.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length} message(s)");

    JsonElement kept = await Surl("read_retained_message", new() { ["topic"] = "sensors/boiler/temp" });
    Console.WriteLine($"kept: {(kept.TryGetProperty("text", out JsonElement text) ? text.GetString() : "nothing")}");
}
else
{
    await Publish("boiler", "64.0");
    Console.WriteLine($"service: {await Service("boiler", 75)}");
    await Publish("kiln", "75");
    Console.WriteLine($"service: {await Service("kiln", 75)}");
    JsonElement events = await Surl("get_events", new() { ["kinds"] = new[] { "store_changed" }, ["store"] = "mail" });
    Console.WriteLine($"deliveries: {events.GetProperty("events").GetArrayLength()}");
    (_, string list) = await Curl("-s", "-m", "10", pop3);
    Console.WriteLine($"mailbox: {list.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length} message(s)");
    Console.WriteLine($"service: {await Service("pump", 75)}");
}

await Surl("stop_server", new());
return 0;

// The sensor: curl -d publishes at QoS 0, which the broker does not acknowledge, so the call can return before the
// reading is kept. Wait, at most 10 s, for that exchange to end: by then the broker has applied it.
async Task Publish(string sensor, string reading)
{
    await Curl("-s", "-m", "10", "-d", reading, $"{mqtt}sensors/{sensor}/temp");
    await Wait("exchange_ended");
}

// The service under test: reads a sensor's latest reading, trying five times a second apart, and past the limit
// mails ops@example.com. A subscriber is sent the topic's length in two bytes, the topic, then the payload.
async Task<string> Service(string sensor, double limit)
{
    string topic = $"sensors/{sensor}/temp", reading = "";
    for (int attempt = 0; attempt < 5 && reading.Length == 0; attempt++)
    {
        if (attempt > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        (_, string answer) = await Curl("-s", "-m", "10", $"{mqtt}{topic}");
        await Wait("exchange_ended");
        reading = answer.Length > 2 + topic.Length ? answer[(2 + topic.Length)..] : "";
    }

    if (reading.Length == 0)
    {
        return $"no reading from {sensor}";
    }

    if (double.Parse(reading, CultureInfo.InvariantCulture) <= limit)
    {
        return $"no alert for {sensor} at {reading}";
    }

    File.WriteAllText(Path.Combine(here, "alert.eml"),
        $"From: sensors@example.com\r\nTo: ops@example.com\r\nSubject: {sensor} at {reading} C\r\n\r\n{sensor} reads {reading} C, past the limit of {limit} C.\r\n");
    (int code, _) = await Curl("-sS", "-m", "10", "--mail-from", "sensors@example.com", "--mail-rcpt", "ops@example.com", "-T", "alert.eml", smtp);
    return code == 0 ? $"alert sent for {sensor} at {reading}" : $"the alert failed, exit {code}";
}

// surl-mcp answers in structured content; the server is always this one.
async Task<JsonElement> Surl(string tool, Dictionary<string, object?> arguments)
{
    arguments["server_id"] = id;
    CallToolResult result = await surl.CallToolAsync(tool, arguments);
    return result.StructuredContent?.Clone() ?? default;
}

// wait_for_event returns the first matching event after the last one seen, at most 10 s on.
async Task<JsonElement> Wait(string kind, string? store = null)
{
    var arguments = new Dictionary<string, object?> { ["kinds"] = new[] { kind }, ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000 };
    if (store is not null)
    {
        arguments["store"] = store;
    }

    JsonElement waited = await Surl("wait_for_event", arguments);
    if (!waited.TryGetProperty("event", out JsonElement found))
    {
        throw new TimeoutException($"no {kind} event in 10 s");
    }

    lastSequence = found.GetProperty("sequence").GetInt64();
    return found;
}

// curl-mcp's curl tool runs curl with the arguments given; its answer is text, as JSON.
async Task<(int, string)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement output = answer.GetProperty("standardOutput");
    string text = output.GetProperty("content").GetString()!;
    if (output.GetProperty("isBase64").GetBoolean())
    {
        text = Encoding.UTF8.GetString(Convert.FromBase64String(text));
    }

    return (answer.GetProperty("exitCode").GetInt32(), text);
}
