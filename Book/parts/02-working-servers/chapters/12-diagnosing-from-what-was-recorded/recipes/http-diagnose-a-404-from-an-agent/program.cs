// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A client says it got a 404. An agent reads the request lines in the server's recorded exchanges,
// compares each path with what list_files says is in the store, and says whether the file is
// missing or the path is wrong. Run: dotnet run program.cs -- <case>
//   solution   two clients fail, one succeeds: the agent diagnoses both failures
//   case-only  a path that differs from a real one only in case
//   deleted    a file that was there and was removed
//   folder     a request for a folder
//   no-404     every request was answered: nothing to diagnose
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// The server under test, holding the reports a client is meant to fetch.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string>
    {
        ["reports/2026/summary.txt"] = "all green\n",
        ["reports/readme.txt"] = "Reports live here.\n",
    },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The clients: what they ask for is all the agent is told, and only that some of it was a 404.
string[] asked = which switch
{
    "case-only" => ["Reports/2026/summary.txt"],
    "deleted" => ["reports/2026/summary.txt", "reports/2026/summary.txt"],
    "folder" => ["reports/2026"],
    "no-404" => ["reports/2026/summary.txt", "reports/readme.txt"],
    _ => ["report/summary.txt", "reports/2026/summary.txt", "reports/2026/q3.txt"],
};
foreach (string path in asked)
{
    await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
    if (which == "deleted")
    {
        // Between the two requests the test removes the file, as the agent's own tool call.
        await surl.CallToolAsync("delete_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = "/reports/2026/summary.txt" });
    }
}

// The server records an exchange a moment after the client has its answer: ask until every exchange
// that started has ended, for at most 10 s, before reading the record.
for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
{
    var kinds = (await Events()).Select(e => e.GetProperty("kind").GetString()).ToList();
    if (kinds.Count(k => k == "exchange_started") == asked.Length && kinds.Count(k => k == "exchange_ended") == asked.Length) break;
}

// The record: the request lines the server received, paired in order with the status lines it sent.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
List<(string Path, string Status)> seen = [];
foreach (JsonElement exchange in recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    string Joined(string direction) => string.Concat(exchange.GetProperty("chunks").EnumerateArray()
        .Where(c => c.GetProperty("direction").GetString() == direction).Select(c => c.GetProperty("data").GetString()));
    var requests = Regex.Matches(Joined("received"), @"^[A-Z]+ (\S+) HTTP/1\.1", RegexOptions.Multiline);
    var answers = Regex.Matches(Joined("sent"), @"^HTTP/1\.1 (\d{3})", RegexOptions.Multiline);
    for (int i = 0; i < requests.Count && i < answers.Count; i++)
    {
        seen.Add((requests[i].Groups[1].Value, answers[i].Groups[1].Value));
    }
}

int failed = 0;
foreach ((string path, string status) in seen)
{
    if (status != "404") continue;
    failed++;
    Console.WriteLine($"404 for {path}");
    Console.WriteLine("  " + await Diagnose(path));
}

if (which == "deleted") Console.WriteLine("answers for that path: " + string.Join(" then ", seen.Select(r => r.Status)));
if (failed == 0) Console.WriteLine($"no 404 in {seen.Count} recorded requests");
return 0;

// Walk the path from the root. A folder that is not there makes the path wrong; if every folder is
// there, the file is the one thing missing.
async Task<string> Diagnose(string path)
{
    string[] segments = path.Trim('/').Split('/');
    string folder = "";
    for (int i = 0; i < segments.Length; i++)
    {
        CallToolResult listed = await surl.CallToolAsync("list_files", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["path"] = folder == "" ? "/" : folder,
        });
        string[] names = listed.StructuredContent!.Value.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToArray();
        string wanted = segments[i];
        if (names.Contains(wanted, StringComparer.Ordinal))
        {
            folder += "/" + wanted;
            continue;
        }

        string where = folder == "" ? "/" : folder;
        string held = names.Length == 0 ? "nothing" : string.Join(", ", names);
        string? sameName = names.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase));
        string likeness = sameName is null ? "" : $", which differs from {sameName} only in case";
        bool last = i == segments.Length - 1;
        return last
            ? $"the file is missing: {where} holds {held}, not {wanted}{likeness}"
            : $"the path is wrong: {where} holds {held}, no {wanted}{likeness}";
    }

    return "the path names something that is there, a folder, which a request for a file does not take";
}

// The events kept for the server, oldest first.
async Task<JsonElement[]> Events()
{
    CallToolResult result = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "exchange_started", "exchange_ended" },
    });
    return result.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToArray();
}
