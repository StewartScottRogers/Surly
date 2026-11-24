// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks that a Kafka record reaches an MQTT 5 subscriber: surl-mcp serves both on one bus, the curl tool
// produces to shop.orders and subscribes to shop/orders, and surl-mcp's store tools read what was kept.
// Run: dotnet run kafka-to-mqtt.cs -- <case>
//   transfer      produce one record, subscribe over MQTT 5 (the default)
//   store         the same, then read the store under each spelling
//   reverse       publish_message to shop/orders, then consume shop.orders over Kafka
//   no-anonymous  a producer with no login, on a server without --allow-anonymous
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "transfer";

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

// Kafka and MQTT 5 on one bus; the last message under shop/ is kept, and an MQTT 5 subscribe ends once it is sent.
List<string> options = ["--topic", "shop/#;retain", "--mqtt5-disconnect-after-retained"];
if (mode != "no-anonymous")
{
    options.Add("--allow-anonymous");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "kafka://127.0.0.1:0", "mqtt5://127.0.0.1:0" },
    ["arguments"] = options,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string kafka = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string mqtt5 = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

if (mode == "reverse")
{
    // The test's own publish, under the MQTT name, then a Kafka consumer of the Kafka name.
    await Surl("publish_message", new() { ["topic"] = "shop/orders", ["payload"] = "order-1001 placed" });
    Console.WriteLine($"Kafka consumer of shop.orders: [{Output(await Curl("-s", "-S", "-m", "10", kafka + "shop.orders")).Replace("\n", "|")}]");
    return 0;
}

// The producer, then the subscriber.
JsonElement produced = await Curl("-s", "-S", "-m", "10", "-d", "order-1001 placed", kafka + "shop.orders");
Console.WriteLine($"producer exit {produced.GetProperty("exitCode").GetInt32()} {produced.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}".TrimEnd());
if (mode == "no-anonymous")
{
    return 0;
}

Console.WriteLine($"MQTT subscriber of shop/orders: [{Output(await Curl("-s", "-S", "-m", "10", mqtt5 + "shop/orders"))}]");

if (mode == "store")
{
    JsonElement retained = await Surl("read_retained_message", new() { ["topic"] = "shop/orders" });
    Console.WriteLine($"read_retained_message shop/orders: {retained.GetProperty("text").GetString()}");
    JsonElement log = await Surl("read_log", new() { ["topic"] = "shop/orders" });
    Console.WriteLine($"read_log shop/orders: {string.Join(", ", log.GetProperty("records").EnumerateArray().Select(r => $"{r.GetProperty("offset")} {r.GetProperty("text")}"))}");
    CallToolResult dotted = await surl.CallToolAsync("read_log", new Dictionary<string, object?> { ["server_id"] = id, ["topic"] = "shop.orders" });
    Console.WriteLine($"read_log shop.orders: tool error {dotted.IsError}, {dotted.Content.OfType<TextContentBlock>().First().Text}");
}

return 0;

async Task<JsonElement> Surl(string tool, Dictionary<string, object?> arguments)
{
    arguments["server_id"] = id;
    return (await surl.CallToolAsync(tool, arguments)).StructuredContent!.Value;
}

// curl-mcp answers in text, as JSON, also when curl's exit is not 0.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

static string Output(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
