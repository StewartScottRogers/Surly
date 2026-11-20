// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches a numbered series with one globbed command through curl-mcp's curl tool,
// from a server surl-mcp started, and reports the numbers that were missing.
// Run: dotnet run fetch-series.cs -- <case>
//   series      report-[1-12].csv into reports/, naming each missing one (the default)
//   fail-early  the same with --fail-early, which stops at the first missing one
//   hash        -o "reports/report-#1.csv" in place of -O and --output-dir
//   outside     -o "../report-#1.csv", a name that leaves the folder
//   transfer    the globbed URL given to transfer instead
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "series";

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

// The series on a free port, with two gaps: 4 and 9.
var files = Enumerable.Range(1, 12).Except([4, 9]).ToDictionary(n => $"report-{n}.csv", n => $"month,total\n{n},{n * 100}\n");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = files,
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string series = url + "report-[1-12].csv";

// Start from an empty reports/, so that what is in it afterwards is this run's.
if (Directory.Exists("reports")) Directory.Delete("reports", recursive: true);

CallToolResult result;
JsonElement answer;
if (which == "transfer")
{
    result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = series });
    answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"statusCode: {answer.GetProperty("statusCode").GetRawText()}");
}
else
{
    // curl -sf --create-dirs --output-dir reports -O -w "%{onerror}missing %{url.path}\n" URL
    var curlArgs = which switch
    {
        "hash" => new List<string> { "-sf", "--create-dirs", "-o", "reports/report-#1.csv", "-w", "%{onerror}missing %{url.path}\n", series },
        "outside" => new List<string> { "-sf", "-o", "../report-#1.csv", series },
        _ => new List<string> { "-sf", "--create-dirs", "--output-dir", "reports", "-O", "-w", "%{onerror}missing %{url.path}\n", series },
    };
    if (which == "fail-early") curlArgs.Insert(0, "--fail-early");
    result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode: {answer.GetProperty("exitCode").GetRawText()}");
    if (answer.GetProperty("standardOutput").ValueKind == JsonValueKind.Null)
    {
        Console.WriteLine(answer.GetProperty("failure").GetProperty("text").GetString());
        Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
        return 1;
    }

    // The agent's report: one "missing" line per number that was not there.
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString() ?? "";
    var missing = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()["missing ".Length..]);
    Console.WriteLine($"missing: {string.Join(", ", missing)}");
    Console.WriteLine($"saved: {(Directory.Exists("reports") ? Directory.GetFiles("reports").Length : 0)}");
}

JsonElement failure = answer.GetProperty("failure");
Console.WriteLine($"failure: {(failure.ValueKind == JsonValueKind.Null ? "null" : failure.GetProperty("kind").GetString())}");
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
