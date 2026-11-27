// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Three clients share one surl-mcp test server, each given its own listen URL, and run their requests in turns
// through curl-mcp's curl tool. One of them sends malformed requests. The agent reads the record, groups the
// exchanges by the endpoint they arrived on, and names the client whose requests the server could not read.
// Run: dotnet run find-the-client.cs
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

// One server, one store, three endpoints: each client is configured with its own.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "http://127.0.0.1:0", "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["invoices.csv"] = "id,total\n1,42\n", ["index.csv"] = "term,hits\na,3\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string[] endpoints = started.StructuredContent!.Value.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();
string[] names = ["billing", "search", "reports"];

// Their runs, in turns; each request is one curl call, so one connection and one exchange.
string[][][] runs =
[
    [["invoices.csv"], ["missing.csv"], ["invoices.csv"]],
    [["index.csv"], ["-H", "X-Request ID: 7", "index.csv?q=a"], ["-H", "X-Request ID: 8", "index.csv?q=b"], ["index.csv"]],
    [["invoices.csv"], ["index.csv"], ["invoices.csv"]],
];
var exits = new Dictionary<string, List<int>>();
for (int turn = 0; turn < 4; turn++)
{
    for (int client = 0; client < 3; client++)
    {
        if (turn >= runs[client].Length) continue;
        string[] request = runs[client][turn];
        string[] arguments = ["-s", "-m", "10", "-o", "out.txt", .. request[..^1], endpoints[client] + request[^1]];
        CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
        int exit = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("exitCode").GetInt32();
        (exits.TryGetValue(names[client], out var list) ? list : exits[names[client]] = []).Add(exit);
    }
}

Console.WriteLine($"curl exits: {string.Join("; ", names.Select(n => $"{n} {string.Join(" ", exits[n])}"))}");

// The record is whole once every exchange that started has ended: poll for that, at most 10 s.
for (int tries = 0; tries < 50; tries++)
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "exchange_started", "exchange_ended" }, ["max_events"] = 500,
    });
    var all = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToList();
    if (all.Count == 2 * 10 && all.Count(e => e.GetProperty("kind").GetString() == "exchange_ended") == 10) break;
    await Task.Delay(200);
}

// Every exchange, grouped by the endpoint it arrived on.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server, ["max_exchanges"] = 100 });
var exchanges = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().ToList();
foreach (var group in exchanges.GroupBy(x => x.GetProperty("listen_url").GetString()!))
{
    string who = names[Array.IndexOf(endpoints, group.Key)];
    var statuses = group.Select(Status).GroupBy(s => s).OrderBy(s => s.Key).Select(s => $"{s.Key} x{s.Count()}");
    Console.WriteLine($"{group.Key} ({who}): {group.Count()} requests, {string.Join(", ", statuses)}");
}

// A 400 means the server could not read the request as HTTP; a 404 is a well-formed request for a missing path.
var bad = exchanges.Where(x => Status(x) == "400").ToList();
foreach (var group in bad.GroupBy(x => x.GetProperty("listen_url").GetString()!))
{
    string who = names[Array.IndexOf(endpoints, group.Key)];
    Console.WriteLine($"bad requests: {group.Count()}, all from {who}");
    foreach (JsonElement x in group)
    {
        string[] head = Text(x, "received").Split("\r\n\r\n")[0].Split("\r\n");
        Console.WriteLine($"  {head[0]}, with {string.Join(", ", head[1..].Where(l => !l.Split(':')[0].All(c => char.IsAsciiLetterOrDigit(c) || c == '-')))}");
    }
}

// The same answer from one endpoint alone, and why the client address could not have given it.
string search = endpoints[1];
CallToolResult one = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server, ["listen_url"] = search });
var mine = one.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().ToList();
Console.WriteLine($"listen_url {search}: {mine.Count} exchanges, {mine.Count(x => Status(x) == "400")} answered 400");
Console.WriteLine($"client addresses of search's {mine.Count} connections: {mine.Select(x => x.GetProperty("client").GetString()).Distinct().Count()} different, all 127.0.0.1: {mine.All(x => x.GetProperty("client").GetString()!.StartsWith("127.0.0.1:"))}");
return 0;

static string Text(JsonElement exchange, string direction) => string.Concat(exchange.GetProperty("chunks").EnumerateArray()
    .Where(c => c.GetProperty("direction").GetString() == direction).Select(c => c.GetProperty("data").GetString()));

static string Status(JsonElement exchange) => Text(exchange, "sent")[9..12];
