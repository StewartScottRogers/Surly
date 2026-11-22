// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side: two clients fetch one report from a surl-mcp server, one is answered 200 and one
// 400, and the agent puts their two requests side by side, as the server recorded them.
// Run: dotnet run compare-requests.cs [-- no-host | transfer]
//   (no argument)  the bad client writes its header with a space before the colon
//   no-host        the bad client sends no Host header
//   transfer       try the bad header through the transfer tool instead
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "space";

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

// The server: one report in memory on a free port.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["report.csv"] = "id,total\n1,40\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "report.csv";

// transfer checks a header name before it sends anything, so it will not make the bad request.
if (mode == "transfer")
{
    var headers = new JsonArray { (JsonNode)new JsonObject { ["name"] = "X-Api-Key ", ["value"] = "k123" } };
    CallToolResult refusedCall = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url, ["headers"] = headers });
    using JsonDocument refusal = JsonDocument.Parse(refusedCall.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement failure = refusal.RootElement.GetProperty("failure");
    Console.WriteLine($"failure: {failure.GetProperty("kind").GetString()}");
    Console.WriteLine(failure.GetProperty("text").GetString());
    Console.WriteLine($"tool error: {(refusedCall.IsError == true ? "yes" : "no")}");
    return refusedCall.IsError == true ? 1 : 0;
}

// The two clients, through the curl tool, which sends -H headers as typed. Each call is a
// connection, and an exchange, of its own; -o keeps each body in a file under the root.
string[] badHeaders = mode == "no-host" ? ["-H", "X-Api-Key: k123", "-H", "Host:"] : ["-H", "X-Api-Key : k123"];
foreach (var (name, headers) in new[] { ("good", new[] { "-H", "X-Api-Key: k123" }), ("bad", badHeaders) })
{
    string[] argv = ["-s", "-o", $"{name}.out", "-w", "%{http_code}", .. headers, url];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
    using JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    string status = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();
    string body = File.ReadAllText(Path.Combine(here, $"{name}.out")).TrimEnd('\n');
    Console.WriteLine(status == "200"
        ? $"{name} client: 200"
        : $"{name} client: {status}, body {body}, exit {answer.RootElement.GetProperty("exitCode").GetInt32()}");
}

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

// Every exchange the server kept, each with its chunks in the order they crossed.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
var exchanges = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().ToList();
JsonElement Answered(string status) => exchanges.First(e => e.GetProperty("chunks").EnumerateArray()
    .First(c => c.GetProperty("direction").GetString() == "sent").GetProperty("data").GetString()!.StartsWith($"HTTP/1.1 {status} ", StringComparison.Ordinal));
string[] Lines(JsonElement e) => e.GetProperty("chunks").EnumerateArray()
    .Where(c => c.GetProperty("direction").GetString() == "received")
    .SelectMany(c => c.GetProperty("data").GetString()!.Split("\r\n")).Where(l => l.Length > 0).ToArray();
JsonElement ok = Answered("200"), refused = Answered("400");
if (ok.GetProperty("is_cut").GetBoolean() || refused.GetProperty("is_cut").GetBoolean()) { Console.WriteLine("an exchange was cut at 64 KiB"); return 1; }
string[] okLines = Lines(ok), refusedLines = Lines(refused);
Console.WriteLine($"good: exchange {ok.GetProperty("exchange_id").GetInt64()}, answered 200");
Console.WriteLine($"bad:  exchange {refused.GetProperty("exchange_id").GetInt64()}, answered 400");

// The lines only one of the two requests holds, then how many they share.
foreach (string line in okLines.Except(refusedLines)) Console.WriteLine($"- {line}");
foreach (string line in refusedLines.Except(okLines)) Console.WriteLine($"+ {line}");
Console.WriteLine($"same: {okLines.Intersect(refusedLines).Count()} lines");

// The answer the bad request got, head only: the server's word for it.
string head = refused.GetProperty("chunks").EnumerateArray().First(c => c.GetProperty("direction").GetString() == "sent").GetProperty("data").GetString()!;
foreach (string line in head.Split("\r\n").TakeWhile(l => l.Length > 0).Where(l => !l.StartsWith("Date:", StringComparison.Ordinal)))
{
    Console.WriteLine($"< {line}");
}
return 0;
