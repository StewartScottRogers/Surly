// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Three clients use one surl-mcp server, each from a local port of its own; one sends a malformed
// request. The agent sifts get_exchanges for the answer 400 and says which client sent it.
// Run: dotnet run find-the-misbehaving-client.cs -- <billing port> <search port> <reports port>
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var ports = new Dictionary<string, string> { ["billing"] = args[0], ["search"] = args[1], ["reports"] = args[2] };

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

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

// The clients: one curl tool call each, from its own local port. Search's second request has a
// header whose name has a space in it.
foreach ((string name, string port) in ports)
{
    string[] words = name == "search"
        ? ["-s", "--max-time", "30", "--local-port", port, "-w", "%{http_code} ", "--create-dirs", "-o", "out/a.txt", url,
           "--next", "-s", "--max-time", "30", "--local-port", port, "-w", "%{http_code} ", "-H", "X-Request ID: 7", "-o", "out/b.txt", url]
        : ["-s", "--max-time", "30", "--local-port", port, "-w", "%{http_code} ", "--create-dirs", "-o", "out/a.txt", url, "-o", "out/b.txt", url];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{name}: exitCode {run.GetProperty("exitCode").GetInt32()}, {run.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}");
}

// The record: wait (at most 10 s) until every exchange that started has ended, then read it.
async Task<int> Count(string kind)
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = serverId, ["kinds"] = new[] { kind } });
    return events.StructuredContent!.Value.GetProperty("events").GetArrayLength();
}
for (var clock = System.Diagnostics.Stopwatch.StartNew(); await Count("exchange_ended") < await Count("exchange_started");)
{
    if (clock.Elapsed > TimeSpan.FromSeconds(10)) { Console.WriteLine("the exchanges did not end within 10 s"); return 1; }
    await Task.Delay(100);
}
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
var exchanges = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().ToList();
Console.WriteLine($"exchanges: {exchanges.Count}, clients: {string.Join(" ", exchanges.Select(e => e.GetProperty("client").GetString()!.Split(':')[0]).Distinct())}");

// Each exchange's chunks in the order they crossed: remember the last request head received, and
// when an answer of 400 goes out, that request is the malformed one.
foreach (JsonElement exchange in exchanges)
{
    string client = exchange.GetProperty("client").GetString()!;
    string request = "";
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        string data = chunk.GetProperty("data").GetString()!;
        if (chunk.GetProperty("direction").GetString() == "received" && data.StartsWith("GET ", StringComparison.Ordinal))
        {
            request = data;
        }
        else if (chunk.GetProperty("direction").GetString() == "sent" && data.StartsWith("HTTP/1.1 400 ", StringComparison.Ordinal))
        {
            string who = ports.FirstOrDefault(p => client.EndsWith(":" + p.Value, StringComparison.Ordinal)).Key ?? "an unknown client";
            Console.WriteLine($"answered 400: exchange {exchange.GetProperty("exchange_id")}, from {client}, the {who} client");
            foreach (string line in request.Split("\r\n").TakeWhile(l => l.Length > 0))
            {
                Console.WriteLine($"  {line}");
            }
        }
    }
}
return 0;
