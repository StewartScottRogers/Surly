// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reads a short JSON spec of the servers a test needs, starts each one with surl-mcp,
// reports the URLs they bound, and checks each answers. If one will not start, it stops the ones
// it already started, so a half-built environment is never left running.
// Run: dotnet run build-from-spec.cs -- <spec.json>
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string specFile = args.Length > 0 ? args[0] : "environment.json";

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

// 1. Start each server the spec names, in order. Its fields are passed as start_server's arguments.
using JsonDocument spec = JsonDocument.Parse(File.ReadAllText(specFile));
var started = new Dictionary<string, (string Id, string[] Urls)>();
foreach (JsonProperty server in spec.RootElement.GetProperty("servers").EnumerateObject())
{
    CallToolResult result = await surl.CallToolAsync("start_server", server.Value.EnumerateObject().ToDictionary(field => field.Name, field => (object?)field.Value));
    if (result.IsError == true)
    {
        // 1a. All or nothing: stop what this spec started so far, and say which server failed and why.
        Console.WriteLine($"{server.Name}: {result.Content.OfType<TextContentBlock>().Single().Text}");
        foreach ((string name, (string id, _)) in started)
        {
            await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = id });
            Console.WriteLine($"stopped {name}");
        }

        JsonElement left = (await surl.CallToolAsync("list_servers", new Dictionary<string, object?>())).StructuredContent!.Value;
        Console.WriteLine($"left running: {left.GetProperty("servers").GetArrayLength()}");
        return 1;
    }

    JsonElement answer = result.StructuredContent!.Value;
    started[server.Name] = (answer.GetProperty("server_id").GetString()!, answer.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray());
}

// 2. The report: each server's name and the URLs it bound, for the test to read.
foreach ((string name, (_, string[] urls)) in started)
{
    Console.WriteLine($"{name}: {string.Join(" ", urls)}");
}

// 3. A smoke check per server: fetch the web server's health file, mail ann, publish an event.
string web = started["web"].Urls[0], mail = started["mail"].Urls[0], events = started["events"].Urls[0];
JsonElement health = await Curl("transfer", new() { ["url"] = web + "health.txt" });
Console.WriteLine($"web health.txt: {health.GetProperty("statusCode")} {health.GetProperty("body").GetProperty("content").GetString()!.Trim()}");
await Curl("curl", new() { ["args"] = new[] { "-s", "--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", "ann@example.com", "-T", "hello.eml", mail + "example.com" } });
JsonElement inbox = (await surl.CallToolAsync("list_messages", new Dictionary<string, object?> { ["server_id"] = started["mail"].Id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX" })).StructuredContent!.Value;
Console.WriteLine($"mail to ann: {inbox.GetProperty("messages").GetArrayLength()} in INBOX");
await Curl("curl", new() { ["args"] = new[] { "-s", "-u", "app:apppass", "-d", "ready", events + "env/status" } });
// An MQTT publish at QoS 0 is never acknowledged, so curl is done before the broker has kept it: wait for that.
await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?> { ["server_id"] = started["events"].Id, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus", ["name"] = "env/status", ["timeout_ms"] = 10000 });
JsonElement retained = (await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?> { ["server_id"] = started["events"].Id, ["topic"] = "env/status" })).StructuredContent!.Value;
Console.WriteLine($"events env/status: {retained.GetRawText()}");
return 0;

// A curl-mcp tool; it answers in text, as JSON.
async Task<JsonElement> Curl(string tool, Dictionary<string, object?> arguments)
{
    CallToolResult result = await curl.CallToolAsync(tool, arguments);
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
