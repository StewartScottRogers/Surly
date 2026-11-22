// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a CI job's tests against surl-mcp with curl-mcp, over the official MCP SDK. When a
// test fails it keeps what the server recorded in ci-logs/ for the job to upload: the events, and
// the exchanges with every byte a client sent left out.
// Run: dotnet run keep-logs.cs [-- <ftps password> | log-file]
//   <ftps password>  the password the FTPS test logs in with (the right one, s3cret, by default)
//   log-file         ask start_server for a log file instead, and print its answer
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string ftpsPassword = args.Length > 0 ? args[0] : "s3cret";

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

if (ftpsPassword == "log-file")
{
    CallToolResult refused = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "https://127.0.0.1:0" },
        ["arguments"] = new[] { "--log-file", "surl.log" },
    });
    Console.WriteLine($"tool error: {refused.IsError}: {refused.Content.OfType<TextContentBlock>().Single().Text}");
    return 1;
}

// The server: a password account and a Bearer token (the empty user name), over HTTPS and FTPS,
// with the certificate and key setup made in this folder.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0", "ftps://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "app:s3cret", "--user", ":tok-7f3a9c" },
    ["files"] = new Dictionary<string, string> { ["report.csv"] = "id,total\n1,40\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string https = server.GetProperty("bound_urls")[0].GetString()!, ftps = server.GetProperty("bound_urls")[1].GetString()!;

// The tests, through curl-mcp's curl tool: the first failure ends the run.
bool passed =
    await Test("https password", "-u", "app:s3cret", https + "report.csv")
    && await Test("https token", "--oauth2-bearer", "tok-7f3a9c", https + "report.csv")
    && await Test("ftps password", "-u", "app:" + ftpsPassword, ftps + "report.csv");
if (passed)
{
    return 0;
}

// The run failed. The server records the end of the last exchange a moment after its client is done:
// ask again until every exchange it started has ended, for at most 10 s, before keeping the record.
for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
{
    CallToolResult ends = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["kinds"] = new[] { "exchange_started", "exchange_ended" }, ["max_events"] = 500,
    });
    if (ends.StructuredContent!.Value.GetProperty("events").EnumerateArray().Sum(e => e.GetProperty("kind").GetString() == "exchange_started" ? 1 : -1) == 0)
    {
        break;
    }
}

// The events name each login's method and account, never its secret: keep them whole.
Directory.CreateDirectory("ci-logs");
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = serverId });
File.WriteAllText("ci-logs/events.json", events.StructuredContent!.Value.GetRawText());

// The exchanges hold every byte both ways: keep what the server sent, and only a count of what it received.
CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
var kept = new StringBuilder();
bool passwordRecorded = false;
foreach (JsonElement exchange in exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    kept.Append($"exchange {exchange.GetProperty("exchange_id")} {exchange.GetProperty("scheme")}\n");
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        string data = chunk.GetProperty("data").GetString()!;
        if (chunk.GetProperty("direction").GetString() == "sent")
        {
            kept.Append($"> {data.TrimEnd('\r', '\n')}\n");
        }
        else
        {
            passwordRecorded |= data.Contains("PASS " + ftpsPassword);
            kept.Append($"< {data.Length} {chunk.GetProperty("encoding")} characters received\n");
        }
    }
}

File.WriteAllText("ci-logs/exchanges.txt", kept.ToString());
Console.WriteLine($"recorded exchanges hold the password: {passwordRecorded}");
Console.WriteLine("kept ci-logs/events.json, ci-logs/exchanges.txt");
return 1;

// One test: curl -sS --cacert server.pem <login> <url>, through the curl tool; prints the exit code.
async Task<bool> Test(string label, string option, string value, string url)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "-f", "--cacert", "server.pem", option, value, "-o", "report.csv", url },
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{label}: exit {answer.GetProperty("exitCode")}");
    return result.IsError != true;
}
