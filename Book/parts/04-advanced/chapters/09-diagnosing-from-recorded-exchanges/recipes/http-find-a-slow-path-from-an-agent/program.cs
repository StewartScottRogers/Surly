// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test run fetches three paths, one of them slow; the agent then reads what the server recorded and
// says which path took longest. Run: dotnet run program.cs [-- <case>]
//   one-each        the test fetches each path with its own transfer call (the default)
//   one-connection  the test fetches all three with one curl call, over one kept-alive connection
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "one-each";

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

// The server under test: /reports/weekly.csv is held 1.5 s before every answer.
var files = new Dictionary<string, string>
{
    ["index.html"] = "<p>reports</p>\n",
    ["reports/daily.csv"] = "day,total\nmon,3\n",
    ["reports/weekly.csv"] = "week,total\n47,21\n",
};
JsonElement server = (await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--fault", "delay;stage=command;path=/reports/weekly.csv;ms=1500" },
    ["files"] = files,
})).StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!;

// The first transfer curl-mcp makes sets its engine up: make it against a server of its own.
JsonElement spare = (await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["ready.txt"] = "ready\n" },
})).StructuredContent!.Value;
await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = spare.GetProperty("bound_urls")[0].GetString() + "ready.txt", ["timeoutSeconds"] = 10 });

// The test run. Every call is bounded at 10 s.
string[] paths = ["index.html", "reports/weekly.csv", "reports/daily.csv"];
if (which == "one-connection")
{
    var curlArgs = new List<string> { "-s", "--max-time", "10" };
    foreach (string path in paths) curlArgs.AddRange(["-o", "got/" + path, "--create-dirs", url + path]);
    await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
}
else
{
    foreach (string path in paths)
    {
        await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path, ["timeoutSeconds"] = 10 });
    }
}

// The agent's part. get_events: when each exchange (for HTTP, a connection) started and ended.
// An exchange ends once the server sees its connection close, a moment after the client is done:
// ask again until every started exchange has ended, 5 s at most.
JsonElement events = default;
for (int tries = 0; tries < 50; tries++)
{
    events = (await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = id,
        ["kinds"] = new[] { "exchange_started", "exchange_ended", "fault_applied" },
    })).StructuredContent!.Value.GetProperty("events").Clone();
    int open = events.EnumerateArray().Sum(e => e.GetProperty("kind").GetString() switch { "exchange_started" => 1, "exchange_ended" => -1, _ => 0 });
    if (open == 0) break;
    await Task.Delay(100);
}
var started = new Dictionary<int, DateTimeOffset>();
var ended = new Dictionary<int, DateTimeOffset>();
var held = new List<string>();
foreach (JsonElement e in events.EnumerateArray())
{
    int exchange = e.GetProperty("exchange_id").GetInt32();
    DateTimeOffset at = e.GetProperty("at").GetDateTimeOffset();
    switch (e.GetProperty("kind").GetString())
    {
        case "exchange_started": started[exchange] = at; break;
        case "exchange_ended": ended[exchange] = at; break;
        case "fault_applied": held.Add($"{e.GetProperty("target")} (exchange {exchange})"); break;
    }
}

// get_exchanges: what each exchange carried; every request line a client sent starts a received chunk.
JsonElement exchanges = (await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id }))
    .StructuredContent!.Value.GetProperty("exchanges");
var rows = new List<(int Exchange, string Requests, TimeSpan Took)>();
foreach (JsonElement x in exchanges.EnumerateArray())
{
    int exchange = x.GetProperty("exchange_id").GetInt32();
    IEnumerable<string> requests = x.GetProperty("chunks").EnumerateArray()
        .Where(c => c.GetProperty("direction").GetString() == "received" && c.GetProperty("encoding").GetString() == "text")
        .Select(c => c.GetProperty("data").GetString()!.Split("\r\n")[0])
        .Where(line => line.EndsWith(" HTTP/1.1"))
        .Select(line => line[..^9]);
    if (started.TryGetValue(exchange, out DateTimeOffset from) && ended.TryGetValue(exchange, out DateTimeOffset to))
    {
        rows.Add((exchange, string.Join(", ", requests), to - from));
    }
}

static string Range(TimeSpan t) => t.TotalSeconds switch { < 1 => "under 1 s", >= 1.5 and < 4 => "1.5 to 4 s", var s => $"{s:F1} s" };
foreach ((int exchange, string requests, TimeSpan took) in rows)
{
    Console.WriteLine($"exchange {exchange}: {requests}: {Range(took)}");
}

var slowest = rows.MaxBy(r => r.Took);
Console.WriteLine($"slowest: {slowest.Requests}");
Console.WriteLine($"held by a fault: {string.Join(", ", held)}");
return 0;
