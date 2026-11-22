// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side: one surl-mcp server listens on two HTTP URLs and an FTP one, three clients
// fetch a report, and the agent asks which listener each exchange came through.
// Run: dotnet run which-listener.cs [-- no-anonymous]
//   (no argument)  the three clients, then what the server recorded
//   no-anonymous   the FTP fetch from a server started without --allow-anonymous
#:package ModelContextProtocol.Core@*

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

// One server, three listeners on free ports, one report; --allow-anonymous opens FTP to anyone.
bool anonymous = !(args.Length > 0 && args[0] == "no-anonymous");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "http://127.0.0.1:0", "ftp://127.0.0.1:0" },
    ["arguments"] = anonymous ? new[] { "--allow-anonymous" } : Array.Empty<string>(),
    ["files"] = new Dictionary<string, string> { ["report.csv"] = "id,total\n1,40\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string[] bound = started.StructuredContent!.Value.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();

if (!anonymous)
{
    CallToolResult refused = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = $"{bound[2]}report.csv" });
    using JsonDocument refusal = JsonDocument.Parse(refused.Content.OfType<TextContentBlock>().Single().Text);
    Console.WriteLine($"ftp without --allow-anonymous: exitCode {refusal.RootElement.GetProperty("failure").GetProperty("exitCode").GetInt32()}, tool error: {(refused.IsError == true ? "yes" : "no")}");
    return 1;
}
for (int i = 0; i < bound.Length; i++) Console.WriteLine($"listener {i + 1}: {bound[i]}");

// The clients: the app, meant for listener 1 and configured with 2; an FTP download from 3; and a
// client that connects to 2 with a Host naming 1 (only the curl tool takes -H Host as given).
foreach (string url in new[] { $"{bound[1]}report.csv", $"{bound[2]}report.csv" })
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
    using JsonDocument answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text);
    Console.WriteLine($"client {url}: status {answer.RootElement.GetProperty("statusCode").GetInt32()}");
}
string firstHost = new Uri(bound[0]).Authority;
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-o", "lied.out", "-w", "%{http_code}", "-H", $"Host: {firstHost}", $"{bound[1]}report.csv" },
});
using (JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text))
{
    Console.WriteLine($"client {bound[1]}report.csv with Host {firstHost}: status {answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}");
}

// The record is whole once every exchange that started has ended: wait for that, at most 10 s.
async Task<JsonElement[]> Events(string kind)
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = serverId, ["kinds"] = new[] { kind } });
    return events.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToArray();
}
for (var clock = System.Diagnostics.Stopwatch.StartNew(); (await Events("exchange_ended")).Length < (await Events("exchange_started")).Length;)
{
    if (clock.Elapsed > TimeSpan.FromSeconds(10)) { Console.WriteLine("the exchanges did not end within 10 s"); return 1; }
    await Task.Delay(100);
}

// Each exchange's listen URL, from its exchange_started event, beside the Host its request carried.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
var heads = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().ToDictionary(
    e => e.GetProperty("exchange_id").GetInt64(),
    e => string.Concat(e.GetProperty("chunks").EnumerateArray().Where(c => c.GetProperty("direction").GetString() == "received").Select(c => c.GetProperty("data").GetString())));
foreach (JsonElement e in await Events("exchange_started"))
{
    long id = e.GetProperty("exchange_id").GetInt64();
    string? host = heads[id].Split("\r\n").FirstOrDefault(l => l.StartsWith("Host: ", StringComparison.Ordinal));
    Console.WriteLine($"exchange {id}: {e.GetProperty("listen_url").GetString()}, scheme {e.GetProperty("scheme").GetString()}{(host is null ? "" : $", {host}")}");
}

// get_exchanges filtered by listen_url answers one listener's exchanges alone.
for (int i = 0; i < 2; i++)
{
    CallToolResult one = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId, ["listen_url"] = bound[i] });
    var ids = one.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Select(x => x.GetProperty("exchange_id").GetInt64()).ToList();
    Console.WriteLine($"listener {i + 1}: {(ids.Count == 0 ? "no exchanges" : "exchanges " + string.Join(", ", ids))}");
}
return 0;
