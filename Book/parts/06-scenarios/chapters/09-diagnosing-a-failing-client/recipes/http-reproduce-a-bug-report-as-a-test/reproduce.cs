// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A bug report says: "curl --digest -u app:s3cret -X PUT -d @report.csv http://host/reports/today.csv stores the
// report as one line." The agent runs that command through curl-mcp's curl tool against a surl-mcp stand-in, checks
// what the server stored against the file, and keeps the recorded exchange as evidence. As a test it exits 0 when the
// stored copy equals the file, 1 when the report is reproduced, 2 when the stand-in could not show either.
// Run: dotnet run reproduce.cs -- <case>
//   reported      the command as the report gives it (the default)
//   data-binary   the same command with --data-binary in place of -d
//   no-uploads    the reported command against a stand-in started without --allow-uploads
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "reported";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The report's file is here.
string here = Directory.GetCurrentDirectory();
File.WriteAllText("report.csv", "id,total\n1,42\n2,7\n");
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

// The stand-in for the reporter's server: the account the command logs in with, and uploads allowed.
string[] options = which == "no-uploads" ? ["--user", "app:s3cret"] : ["--user", "app:s3cret", "--allow-uploads"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = options,
    ["files"] = new Dictionary<string, string> { ["reports/README.txt"] = "Reports land here.\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The command from the report, with the stand-in's URL; -s -S -m 10 only keep it quiet and bounded.
string data = which == "data-binary" ? "--data-binary" : "-d";
string[] command = ["--digest", "-u", "app:s3cret", "-X", "PUT", data, "@report.csv", url + "reports/today.csv"];
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = (string[])["-s", "-S", "-m", "10", "-w", "%{http_code}", .. command] });
JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string status = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
Console.WriteLine($"curl {string.Join(' ', command[..^1])} .../reports/today.csv: exit {answer.GetProperty("exitCode")}, status {status[^3..]}");

// Wait, at most 10 s, until surl has ended the exchange, then keep it as evidence, a line per chunk as it crossed.
CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
});
long exchangeId = ended.StructuredContent!.Value.GetProperty("event").GetProperty("exchange_id").GetInt64();
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
{
    ["server_id"] = server, ["after_exchange_id"] = exchangeId - 1, ["max_exchanges"] = 1,
});
JsonElement exchange = recorded.StructuredContent!.Value.GetProperty("exchanges")[0];
Directory.CreateDirectory("evidence");
string evidence = Path.Combine("evidence", $"{which}-exchange-{exchangeId}.txt");
var kept = new StringBuilder($"# {string.Join(' ', command)}\n");
foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
{
    kept.Append($"{chunk.GetProperty("at").GetString()} {(chunk.GetProperty("direction").GetString() == "received" ? "client" : "server")}: {Quoted(chunk.GetProperty("data").GetString())}\n");
}

File.WriteAllText(evidence, kept.ToString());
Console.WriteLine($"evidence: {evidence.Replace('\\', '/')}, {exchange.GetProperty("chunks").GetArrayLength()} chunks");

// What the client sent: the last request of the exchange (Digest answers a challenge first), its head and body.
string sent = string.Concat(exchange.GetProperty("chunks").EnumerateArray().Where(c => c.GetProperty("direction").GetString() == "received").Select(c => c.GetProperty("data").GetString()));
string last = sent[sent.LastIndexOf("PUT ", StringComparison.Ordinal)..];
string[] head = last[..last.IndexOf("\r\n\r\n", StringComparison.Ordinal)].Split("\r\n");
string body = last[(last.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
Console.WriteLine($"request: {head[0]}; {head.First(h => h.StartsWith("Content-Type: "))}; body {Quoted(body)}");

// The test: the stored copy must equal the file.
string expected = File.ReadAllText("report.csv");
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = "/reports/today.csv" });
if (read.IsError == true)
{
    Console.WriteLine($"not reproduced: the stand-in stored nothing ({read.Content.OfType<TextContentBlock>().First().Text.Trim()}); it answered {status[^3..]}");
    return 2;
}

string stored = read.StructuredContent!.Value.GetProperty("text").GetString()!;
Console.WriteLine($"stored: {Quoted(stored)}, {stored.Count(c => c == '\n')} line feeds; report.csv has {expected.Count(c => c == '\n')}");
if (stored == expected)
{
    Console.WriteLine("PASS: the stored copy equals report.csv");
    return 0;
}

Console.WriteLine($"REPRODUCED: the stored copy differs from report.csv; the request body {(body == stored ? "was already the stored copy, so the client sent it so" : "differs from the stored copy")}");
return 1;

// A string as it would be written in C#, so line feeds show.
static string Quoted(string? text) => "\"" + (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
