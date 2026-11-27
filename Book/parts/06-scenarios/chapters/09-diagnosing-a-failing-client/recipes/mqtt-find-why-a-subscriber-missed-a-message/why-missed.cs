// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Sensors publish readings and dashboards subscribe later, all through curl-mcp's curl tool against a surl-mcp MQTT
// broker with grants and a retain filter. For each reading a dashboard missed, the agent checks the subscriber's
// SUBSCRIBE and SUBACK, the retained message, the bus events for the topic and the publisher's PUBLISH, and says why.
// Run: dotnet run why-missed.cs [no-plaintext]   (no-plaintext: a broker without --allow-plaintext-auth, one publish)
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

bool clear = args.Length == 0 || args[0] != "no-plaintext";

// The broker: temperatures are retained; sensor1 may publish on line 1 only; dash may subscribe to the plant; guest to nothing.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
    ["arguments"] = (string[])
    [
        "--user", "sensor1:s1pass", "--user", "dash:dashpass", "--user", "guest:guestpass", .. (clear ? ["--allow-plaintext-auth"] : Array.Empty<string>()),
        "--topic", "plant/+/temp;retain", "--topic-access", "plant/line1/#=sensor1:pub", "--topic-access", "plant/#=dash:sub",
    ],
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string broker = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
long lastSequence = 0;

if (!clear)
{
    // Without --allow-plaintext-auth a password over mqtt:// is refused unchecked: CONNACK return code 5.
    JsonElement refused = default;
    long id = await Client("sensor1:s1pass", "plant/line1/temp", "21.5", a => refused = a);
    var connack = Packets(await Exchange(id)).First(p => p.Type == 2);
    Console.WriteLine($"sensor1 publishing: curl exit {refused.GetProperty("exitCode")}, {refused.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}; CONNACK return code {connack.Body[1]}");
    return 0;
}

// The sensor publishes three readings; each publish is one connection. Then the dashboards subscribe.
await Client("sensor1:s1pass", "plant/line1/temp", "21.5");
await Client("sensor1:s1pass", "plant/line2/temp", "19.0");
await Client("sensor1:s1pass", "plant/line1/humidity", "40");
await Missed("plant/line1/temp", "dash:dashpass", "plant/line1/temp");
await Missed("plant/line2/temp", "dash:dashpass", "plant/line2/temp");
await Missed("plant/line1/humidity", "dash:dashpass", "plant/line1/humidity");
await Missed("plant/line1/temp", "guest:guestpass", "plant/line1/temp");
await Missed("plant/line1/temp", "dash:dashpass", "plant/line1/tmp");
return 0;

// One curl call: a publish with -d, else a subscribe. Waits (at most 10 s) for its exchange to end and returns its id.
async Task<long> Client(string login, string topic, string? payload, Action<JsonElement>? seen = null)
{
    string[] arguments = payload is null ? ["-s", "-S", "-m", "10", "-u", login, broker + topic] : ["-s", "-S", "-m", "10", "-u", login, "-d", payload, broker + topic];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    seen?.Invoke(JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone());
    CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "exchange_ended" }, ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
    });
    JsonElement end = ended.StructuredContent!.Value.GetProperty("event");
    lastSequence = end.GetProperty("sequence").GetInt64();
    return end.GetProperty("exchange_id").GetInt64();
}

// The diagnosis for one reading a subscriber expected on `topic`.
async Task Missed(string topic, string login, string filter)
{
    JsonElement answer = default;
    long subscriber = await Client(login, filter, null, a => answer = a);
    JsonElement output = answer.GetProperty("standardOutput");
    int got = output.GetProperty("totalBytes").GetInt32();
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    Console.WriteLine($"{login.Split(':')[0]} on {filter}: curl exit {answer.GetProperty("exitCode")}, {got} bytes{(error == "" ? "" : ", " + error)}");

    // 1. The subscriber's side: what it asked for and what the broker granted (SUBACK 0x80 is a refusal).
    var (subscribed, granted) = Packets(await Exchange(subscriber)).Aggregate(("", -1), (acc, p) =>
        p.Type == 8 && p.Direction == "received" ? (Topic(p.Body, 2), acc.Item2) : p.Type == 9 && p.Direction == "sent" ? (acc.Item1, p.Body[2]) : acc);
    Console.WriteLine($"  SUBSCRIBE {subscribed}, SUBACK 0x{granted:X2}");
    if (granted == 0x80) { Console.WriteLine($"  why: the subscribe was refused; {login.Split(':')[0]} may not subscribe to {subscribed}"); return; }

    // 2. The store: is a message kept on the topic it expected?
    CallToolResult retained = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?> { ["server_id"] = server, ["topic"] = topic });
    JsonElement kept = retained.StructuredContent!.Value;
    Console.WriteLine($"  retained on {topic}: {(kept.TryGetProperty("text", out JsonElement text) ? text.GetString() : "nothing")}");
    if (subscribed != topic && kept.TryGetProperty("text", out _)) { Console.WriteLine($"  why: it subscribed to {subscribed}, not {topic}"); return; }
    if (got > 0) { Console.WriteLine("  why: nothing missed"); return; }

    // 3. The bus events for the topic, and 4. who published on it, from the PUBLISH packets the broker received.
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus", ["name"] = topic,
    });
    var changes = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("change").GetString()).ToList();
    Console.WriteLine($"  bus events for {topic}: {(changes.Count == 0 ? "none" : string.Join(", ", changes))}");
    string publisher = "nobody";
    for (long id = 1; id < subscriber; id++)
    {
        if (Packets(await Exchange(id)).Any(p => p.Type == 3 && p.Direction == "received" && Topic(p.Body, 0) == topic))
        {
            publisher = await Account(id);
        }
    }

    Console.WriteLine($"  PUBLISH on {topic} from: {publisher}");
    Console.WriteLine("  why: " + (publisher == "nobody" ? "no client published on it"
        : changes.Count == 0 ? $"the broker dropped the publish; {publisher} may not publish on {topic}"
        : !changes.Contains("written") ? "the broker accepted the publish but did not retain it, so a subscriber that comes later gets nothing"
        : "a message is kept; read the subscribe"));
}

async Task<JsonElement> Exchange(long id)
{
    CallToolResult r = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server, ["after_exchange_id"] = id - 1, ["max_exchanges"] = 1 });
    return r.StructuredContent!.Value.GetProperty("exchanges")[0].Clone();
}

async Task<string> Account(long id)
{
    CallToolResult r = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = server, ["kinds"] = new[] { "login_judged" } });
    return r.StructuredContent!.Value.GetProperty("events").EnumerateArray().First(e => e.GetProperty("exchange_id").GetInt64() == id).GetProperty("account").GetString()!;
}

// MQTT control packets, each way: the chunks' bytes joined in order, then cut at each fixed header.
static List<(string Direction, int Type, byte[] Body)> Packets(JsonElement exchange)
{
    var packets = new List<(string, int, byte[])>();
    foreach (string direction in new[] { "received", "sent" })
    {
        byte[] bytes = exchange.GetProperty("chunks").EnumerateArray().Where(c => c.GetProperty("direction").GetString() == direction)
            .SelectMany(c => c.GetProperty("encoding").GetString() == "base64" ? Convert.FromBase64String(c.GetProperty("data").GetString()!) : Encoding.UTF8.GetBytes(c.GetProperty("data").GetString()!)).ToArray();
        for (int at = 0; at < bytes.Length;)
        {
            int type = bytes[at] >> 4, length = 0, shift = 0, i = at + 1;
            do { length |= (bytes[i] & 0x7F) << shift; shift += 7; } while ((bytes[i++] & 0x80) != 0);
            packets.Add((direction, type, bytes[i..(i + length)]));
            at = i + length;
        }
    }

    return packets;
}

// A topic or filter: two bytes of length, then UTF-8 (in a SUBSCRIBE after the two-byte packet identifier).
static string Topic(byte[] body, int at) => Encoding.UTF8.GetString(body, at + 2, body[at] << 8 | body[at + 1]);
