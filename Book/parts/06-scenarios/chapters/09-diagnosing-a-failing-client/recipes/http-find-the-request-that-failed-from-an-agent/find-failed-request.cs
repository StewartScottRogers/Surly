// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side: a client run of fifty requests against a surl-mcp server, then the recorded
// exchanges narrowed to each answer of 400 or more and the request that caused it.
// Run: dotnet run find-failed-request.cs [-- clean]
//   (no argument)  fetch reports 1 to 50; report 37 is missing
//   clean          fetch reports 1 to 36, which are all there
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

int last = args.Length > 0 && args[0] == "clean" ? 36 : 50;

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

// The server: reports 1 to 50 but 37, in memory, on a free port.
var files = Enumerable.Range(1, 50).Where(n => n != 37).ToDictionary(n => $"reports/report-{n}.csv", n => $"id,total\n{n},40\n");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = files,
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The client run: one curl tool call over a URL glob, a status line per request.
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "--create-dirs", "-o", "got/#1.csv", "-w", "%{http_code}\n", $"{baseUrl}reports/report-[1-{last}].csv" },
});
using JsonDocument run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
string[] codes = run.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!
    .Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
Console.WriteLine($"run: {codes.Length} requests, {codes.Count(c => c == "200")} answered 200, exit {run.RootElement.GetProperty("exitCode").GetInt32()}");

// The record is whole once every exchange that started has ended: wait for that, at most 10 s.
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

// Walk every exchange's chunks in the order they crossed: a received chunk that starts with a
// request line is a request; a sent chunk that starts with a status of 400 or more is a failing answer.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
var exchanges = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().ToList();
Console.WriteLine($"exchanges: {exchanges.Count}");
int requests = 0, failed = 0;
foreach (JsonElement exchange in exchanges)
{
    // get_exchanges keeps at most 64 KiB of an exchange; past that the record is cut.
    if (exchange.GetProperty("is_cut").GetBoolean()) Console.WriteLine($"exchange {exchange.GetProperty("exchange_id").GetInt64()} is cut at 64 KiB");
    string[] request = [];
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        string[] lines = chunk.GetProperty("data").GetString()!.Split("\r\n");
        string direction = chunk.GetProperty("direction").GetString()!;
        if (direction == "received" && lines[0].EndsWith(" HTTP/1.1", StringComparison.Ordinal))
        {
            requests++;
            request = lines.TakeWhile(l => l.Length > 0).ToArray();
        }
        else if (direction == "sent" && lines[0].StartsWith("HTTP/1.1 ", StringComparison.Ordinal) && int.Parse(lines[0].Split(' ')[1]) >= 400)
        {
            failed++;
            Console.WriteLine($"request {requests} of the run, exchange {exchange.GetProperty("exchange_id").GetInt64()}");
            foreach (string line in request) Console.WriteLine($"> {line}");
            foreach (string line in lines.TakeWhile(l => l.Length > 0).Where(l => !l.StartsWith("Date: ", StringComparison.Ordinal)))
            {
                Console.WriteLine($"< {line}");
            }
            // Why: ask the server's store for the path the request named.
            CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = request[0].Split(' ')[1] });
            Console.WriteLine($"read_file: {(read.IsError == true ? read.Content.OfType<TextContentBlock>().Single().Text : "the file is there")}");
            Console.WriteLine();
        }
    }
}
Console.WriteLine($"{requests} requests, {failed} answered 400 or more");
return failed > 0 ? 0 : 1;
