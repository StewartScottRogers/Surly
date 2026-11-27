// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent joins two teams' names for one stream: surl-mcp runs NATS and MQTT on one bus with a topic
// map from the NATS subject orders.created to the MQTT topic shop/orders/new, and curl-mcp's curl tool
// plays each team's client.
// Run: dotnet run map-topics.cs -- <case>
//   forth     a NATS publish to orders.created, read back as the MQTT topic shop/orders/new (the default)
//   back      an MQTT publish to shop/orders/new, received by a NATS subscriber to orders.created
//   nomap     the same NATS publish with no map: where it lands
//   wildcard  a map whose NATS side holds a wildcard
//   locked    the map without --allow-anonymous: a NATS publish with no login
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "forth";

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

// NATS and MQTT on one bus, every publish kept for a later subscriber, the two names joined.
var arguments = new List<string> { "--topic", "#;retain" };
if (mode != "locked")
{
    arguments.Add("--allow-anonymous");
}

if (mode != "nomap")
{
    arguments.AddRange(["--topic-map", mode == "wildcard" ? "nats:orders.*=mqtt:shop/orders/new" : "nats:orders.created=mqtt:shop/orders/new"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "nats://127.0.0.1:0", "mqtt://127.0.0.1:0" },
    ["arguments"] = arguments,
});
if (started.IsError == true)
{
    JsonElement refusal = started.StructuredContent!.Value;
    Console.WriteLine($"start_server refused, exit {refusal.GetProperty("exit_code").GetInt32()}: {refusal.GetProperty("lines")[0].GetString()}");
    return 2;
}

string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string nats = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string mqtt = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

switch (mode)
{
    case "forth" or "locked":
        JsonElement published = await Curl("-s", "-S", "-d", "paid", nats + "orders.created");
        if (published.GetProperty("exitCode").GetInt32() != 0)
        {
            Console.WriteLine($"NATS publish refused: exit {published.GetProperty("exitCode").GetInt32()}, {published.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd()}");
            return 1;
        }

        // A NATS publish is not acknowledged: wait for the bus to keep it, then read it two ways.
        await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus", ["name"] = "shop/orders/new", ["timeout_ms"] = 10000,
        });
        Console.WriteLine($"retained on shop/orders/new: {await Retained("shop/orders/new")}");
        JsonElement read = await Curl("-s", "-S", mqtt + "shop/orders/new");
        // The subscriber writes the topic's length in two bytes, the topic, then the payload.
        string written = read.GetProperty("standardOutput").GetProperty("content").GetString()!;
        Console.WriteLine($"MQTT subscriber, exit {read.GetProperty("exitCode").GetInt32()}: topic {written[2..17]}, payload {written[17..]}");
        break;
    case "nomap":
        await Curl("-s", "-S", "-d", "paid", nats + "orders.created");
        await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus", ["timeout_ms"] = 10000,
        });
        Console.WriteLine($"retained on orders/created: {await Retained("orders/created")}");
        Console.WriteLine($"retained on shop/orders/new: {await Retained("shop/orders/new")}");
        break;
    case "back":
        // NATS keeps nothing for a late subscriber: start the subscriber, for one message, and publish
        // over MQTT every half second until it has one, for at most ten seconds.
        Task<JsonElement> subscriber = Curl("-s", "-S", "-m", "10", nats + "orders.created?count=1");
        for (int tries = 0; !subscriber.IsCompleted && tries < 20; tries++)
        {
            await Curl("-s", "-S", "-d", "shipped", mqtt + "shop/orders/new");
            await Task.WhenAny(subscriber, Task.Delay(500));
        }

        JsonElement got = await subscriber;
        Console.Write($"NATS subscriber, exit {got.GetProperty("exitCode").GetInt32()}: {got.GetProperty("standardOutput").GetProperty("content").GetString()}");
        break;
}

return 0;

// The curl tool runs curl's own arguments and answers exitCode, standardOutput and standardError.
async Task<JsonElement> Curl(params string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// read_retained_message reads the bus by its MQTT topic name: the text kept, or retained false.
async Task<string> Retained(string topic)
{
    CallToolResult result = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?> { ["server_id"] = id, ["topic"] = topic });
    JsonElement kept = result.StructuredContent!.Value;
    return kept.TryGetProperty("text", out JsonElement text) ? text.GetString()! : "(nothing)";
}
