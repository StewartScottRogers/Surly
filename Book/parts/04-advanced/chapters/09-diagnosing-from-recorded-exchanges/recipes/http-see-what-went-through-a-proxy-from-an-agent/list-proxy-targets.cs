// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An app should reach one host only, through a test proxy. The agent reads the proxy listener's
// exchanges from surl-mcp and lists every target the app asked the proxy for.
// Run: dotnet run list-proxy-targets.cs
#:package ModelContextProtocol.Core@2.4.0

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

// One server, three listeners: the app's host, another host, and the proxy (bound_urls in that order).
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--proxy", "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["orders.csv"] = "id,total\n1,40\n", ["stock.csv"] = "sku,count\nA1,4\n", ["collect"] = "ok\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string[] bound = server.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();
string app = new Uri(bound[0]).Authority, other = new Uri(bound[1]).Authority, proxy = bound[2];
string Name(string target) => target == app ? "the app's host" : target == other ? "another host" : target;

// The app's traffic, each request a curl tool call through the proxy (-x). Two are what it should
// do; one goes to another host; one to an address outside loopback; one tunnels with CONNECT (-p).
string[][] calls =
[
    ["-x", proxy, $"http://{app}/orders.csv"],
    ["-x", proxy, $"http://{app}/stock.csv"],
    ["-x", proxy, $"http://{other}/collect"],
    ["-x", proxy, "http://192.0.2.1/beacon"],
    ["-x", proxy, "-p", $"http://{other}/collect"],
];
foreach (string[] call in calls)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", "--max-time", "30", "-o", "out.txt", "-w", "%{http_code}" }.Concat(call).ToArray() });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"app: {call[^1].Replace(app, "<app>").Replace(other, "<other>")} -> {run.GetProperty("standardOutput").GetProperty("content").GetString()}, exitCode {run.GetProperty("exitCode").GetInt32()}");
}

// Wait, at most 10 s, until every exchange that started has ended, so the record is whole.
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

// The proxy listener's exchanges only. A request to a proxy names its target: absolute form
// (GET http://host:port/path) for a forwarded request, CONNECT host:port for a tunnel. The answer
// head is the first sent chunk after it.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId, ["listen_url"] = proxy });
var targets = new List<string>();
Console.WriteLine("through the proxy:");
foreach (JsonElement exchange in recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    string? asked = null;
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        if (chunk.GetProperty("encoding").GetString() != "text") continue;
        string first = chunk.GetProperty("data").GetString()!.Split("\r\n")[0];
        string[] words = first.Split(' ');
        if (chunk.GetProperty("direction").GetString() == "received" && asked is null && words.Length == 3 && words[2].StartsWith("HTTP/"))
        {
            string target = words[0] == "CONNECT" ? words[1] : new Uri(words[1]).Authority;
            string what = words[0] == "CONNECT" ? "CONNECT (a tunnel: no path is seen)" : $"{words[0]} {new Uri(words[1]).AbsolutePath}";
            asked = $"  {Name(target)}: {what}";
            targets.Add(target);
        }
        else if (chunk.GetProperty("direction").GetString() == "sent" && asked is not null && first.StartsWith("HTTP/1.1 "))
        {
            Console.WriteLine($"{asked}, answered {words[1]}");
            asked = null;
            if (first.Contains("Connection established")) break;   // what follows is the tunnel's own traffic
        }
    }
}
var stray = targets.Distinct().Where(t => t != app).Select(Name).ToList();
Console.WriteLine($"targets: {targets.Distinct().Count()}; besides the app's host: {string.Join(", ", stray)}");

// The other host's own record shows the paths, the tunnelled one included: a forwarded request
// carries Via: 1.1 surl, a tunnelled one does not.
CallToolResult atOther = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId, ["listen_url"] = bound[1] });
foreach (JsonElement exchange in atOther.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    string head = exchange.GetProperty("chunks").EnumerateArray().First(c => c.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!;
    Console.WriteLine($"another host received: {head.Split("\r\n")[0]}, {(head.Contains("\r\nVia: 1.1 surl\r\n") ? "with Via" : "no Via")}");
}
return 0;
