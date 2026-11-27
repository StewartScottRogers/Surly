// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Two accounts upload export.csv to a surl-mcp FTP server through curl-mcp's curl tool: alice the right file, bob a
// wrong one, then alice one past the size limit. The agent names who stored the file from get_events.
// Run: dotnet run program.cs -- <case>
//   who             who stored /export.csv, and who stored it last
//   uploads         what each upload got back
//   file            what /export.csv holds now
//   refused         the exchanges with a STOR, against the store changes
//   record          the PASS lines in get_exchanges
//   log-file        a start_server that asks for a log file
//   plaintext-off   the same uploads without --allow-plaintext-auth
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args[0];

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The uploads are written here.
string here = Directory.GetCurrentDirectory();
File.WriteAllText("good.csv", "id,total\n1,42\n");
File.WriteAllText("bad.csv", "id,total\n1,0\n");
File.WriteAllText("big.csv", string.Concat(Enumerable.Range(1, 20).Select(i => $"{i},42\n")));
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

// surl-mcp: two accounts, passwords taken over plain FTP, uploads up to 100 bytes.
var serverArguments = new List<string> { "--user", "alice:north-7", "--user", "bob:south-9", "--allow-uploads", "--max-filesize", "100" };
if (which != "plaintext-off") serverArguments.Add("--allow-plaintext-auth");
if (which == "log-file") serverArguments.AddRange(["--log-file", "surl.log"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = serverArguments,
});
if (started.IsError == true)
{
    Console.WriteLine($"start_server: {started.Content.OfType<TextContentBlock>().Single().Text}");
    return 0;
}

string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "export.csv";

// The uploads: each curl tool call is one connection, so one exchange.
string[] results =
[
    await Upload("alice:north-7", "good.csv"),
    await Upload("bob:south-9", "bad.csv"),
    await Upload("alice:north-7", "big.csv"),
];
await Settled();

switch (which)
{
    case "who":
        await Who();
        break;
    case "uploads":
    case "plaintext-off":
        Array.ForEach(results, Console.WriteLine);
        break;
    case "file":
        CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = "/export.csv" });
        Console.Write(read.StructuredContent!.Value.GetProperty("text").GetString());
        break;
    case "refused":
        int asked = (await Received()).Count(text => text.Contains("STOR export.csv\r\n"));
        int stored = (await Events(new() { ["kinds"] = new[] { "store_changed" }, ["name"] = "/export.csv" })).Count;
        Console.WriteLine($"{asked} exchanges with STOR export.csv, {stored} store changes");
        break;
    case "record":
        foreach (string line in (await Received()).SelectMany(text => text.Split("\r\n")).Where(l => l.StartsWith("PASS ")))
        {
            Console.WriteLine(line);
        }

        break;
}

return 0;

// The finding: each store change to the path, joined by exchange_id to the login judged on that exchange.
async Task Who()
{
    var accounts = (await Events(new() { ["kinds"] = new[] { "login_judged" } }))
        .Where(e => e.GetProperty("is_accepted").GetBoolean())
        .ToDictionary(e => e.GetProperty("exchange_id").GetInt64(), e => e.GetProperty("account").GetString()!);
    string last = "";
    foreach (JsonElement change in await Events(new() { ["kinds"] = new[] { "store_changed" }, ["name"] = "/export.csv" }))
    {
        long exchange = change.GetProperty("exchange_id").GetInt64();
        last = accounts[exchange];
        Console.WriteLine($"exchange {exchange}: {last} stored {change.GetProperty("name").GetString()}");
    }

    Console.WriteLine(last == "" ? "no store change to /export.csv" : $"last stored by {last}");
}

// The record is kept as exchanges run: wait, with a bound, until every exchange that started has ended.
async Task Settled()
{
    for (int tries = 0; tries < 50; tries++)
    {
        int startedCount = (await Events(new() { ["kinds"] = new[] { "exchange_started" } })).Count;
        int endedCount = (await Events(new() { ["kinds"] = new[] { "exchange_ended" } })).Count;
        if (startedCount == endedCount) return;
        await Task.Delay(200);
    }
}

// get_events answers the events kept that match, oldest first.
async Task<List<JsonElement>> Events(Dictionary<string, object?> filters)
{
    filters["server_id"] = server;
    CallToolResult result = await surl.CallToolAsync("get_events", filters);
    return [.. result.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.Clone())];
}

// get_exchanges answers each exchange's chunks; the text the client sent is its received chunks joined.
async Task<List<string>> Received()
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
    return [.. result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Select(x => string.Concat(
        x.GetProperty("chunks").EnumerateArray()
            .Where(c => c.GetProperty("direction").GetString() == "received" && c.GetProperty("encoding").GetString() == "text")
            .Select(c => c.GetProperty("data").GetString())))];
}

// The curl tool runs curl's own arguments, one list item each, with no shell. A refusal is an answer with a
// non-zero exitCode, not a failure of the call.
async Task<string> Upload(string login, string file)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "-u", login, "-T", file, url },
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement error = answer.GetProperty("standardError");
    string said = error.ValueKind == JsonValueKind.Null ? "" : error.GetProperty("content").GetString()!.Trim();
    return $"{login.Split(':')[0]} {file}: exit {answer.GetProperty("exitCode")}{(said == "" ? "" : $" {said}")}";
}
