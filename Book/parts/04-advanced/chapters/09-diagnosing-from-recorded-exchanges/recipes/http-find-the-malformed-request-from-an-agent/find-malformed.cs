// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Several clients, each naming itself in User-Agent, use one surl-mcp server; one sends a request
// the server refuses. The agent reads get_exchanges, finds the refused request, names the client
// from the request's own bytes, and points at the line that makes it malformed.
// Run: dotnet run find-malformed.cs
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

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["stock.csv"] = "sku,count\nA1,4\n", ["prices.csv"] = "sku,price\nA1,9.50\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The clients: each a curl tool call that names itself with -A. Inventory adds an API key header
// with a space before the colon; reports asks for a file the server does not have.
(string Agent, string[] Extra, string Path)[] clients =
[
    ("billing/1.4", [], "prices.csv"),
    ("inventory/2.0", ["-H", "X-Api-Key : k-7731"], "stock.csv"),
    ("reports/3.1", [], "summary.csv"),
    ("billing/1.4", [], "stock.csv"),
];
foreach ((string agent, string[] extra, string path) in clients)
{
    string[] words = ["-s", "--max-time", "30", "-A", agent, .. extra, "-o", "out.txt", "-w", "%{http_code}", baseUrl + path];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{agent} /{path}: {run.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}, exitCode {run.GetProperty("exitCode").GetInt32()}");
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

// A field name is a token: letters, digits and !#$%&'*+-.^_`|~ only, nothing else, no spaces.
bool IsToken(string name) => name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c));

// Walk every exchange's chunks: a received chunk is a request head, the sent chunk after it is its answer.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
int malformed = 0;
foreach (JsonElement exchange in recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    string[] request = [];
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        string data = chunk.GetProperty("data").GetString()!;
        if (chunk.GetProperty("direction").GetString() == "received")
        {
            request = data.Split("\r\n").TakeWhile(l => l.Length > 0).ToArray();
            continue;
        }
        if (!data.StartsWith("HTTP/1.1 ", StringComparison.Ordinal) || request.Length == 0) continue;
        int status = int.Parse(data.Split(' ')[1]);
        string agent = request.FirstOrDefault(l => l.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase))?.Split(':', 2)[1].Trim() ?? "(no User-Agent)";
        var bad = request.Skip(1).Select((line, i) => (Line: line, Number: i + 2)).Where(l => !l.Line.Contains(':') || !IsToken(l.Line[..l.Line.IndexOf(':')])).ToList();
        if (status >= 400)
        {
            Console.WriteLine($"exchange {exchange.GetProperty("exchange_id")}: {request[0]} from {agent}, answered {status}");
            Console.WriteLine(bad.Count == 0
                ? "  every header line is well formed"
                : string.Join("\n", bad.Select(b => $"  line {b.Number} is malformed: \"{b.Line}\", its field name \"{b.Line[..b.Line.IndexOf(':')]}\" is not a token")));
        }
        malformed += bad.Count > 0 ? 1 : 0;
        request = [];
    }
}
Console.WriteLine($"malformed requests: {malformed}");
return 0;
