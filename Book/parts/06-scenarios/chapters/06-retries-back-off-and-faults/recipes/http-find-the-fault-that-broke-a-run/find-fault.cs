// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent sets four fault rules on a surl-mcp server, runs a client that fails, and matches the
// faults the server applied to the exchanges that failed. Run: dotnet run find-fault.cs -- <part>
//   match   the run, then each failed request with the rule that broke it, if any (the default)
//   answers the run, then each exchange the server did not answer 2xx, with its faults
//   rules   the run, then each rule with how often it fired
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string part = args.Length > 0 ? args[0] : "match";

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
    ["files"] = new Dictionary<string, string>
    {
        ["index.html"] = "<p>shop</p>\n", ["orders.json"] = "[]\n", ["reports/q3.txt"] = "q3\n", ["export.csv"] = "id\n",
    },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The first transfer in curl-mcp pays a one-time set-up: make it, with room, before the run's -m 1.
await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = baseUrl + "index.html", ["timeoutSeconds"] = 30 });

// The rules a test set up: f1 503 on /orders.json, f2 holds /reports 3 s, f3 drops /export.csv,
// f4 answers /admin 500.
foreach (string fault in new[]
{
    "error;scheme=http;reply=503;path=/orders.json",
    "delay;stage=command;ms=3000;path=/reports",
    "drop;stage=command;path=/export.csv",
    "error;scheme=http;reply=500;path=/admin",
})
{
    await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = server, ["fault"] = fault });
}

// The run: one curl tool call per request, so one connection and one exchange each, every one
// bounded by -m 1. Four of the six fail.
string[] run = ["index.html", "orders.json", "reports/q3.txt", "export.csv", "missing.txt", "index.html"];
var failed = new List<(string Path, string Result)>();
foreach (string path in run)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-m", "1", "-o", "-", "-w", "\n%{http_code} %{exitcode}", baseUrl + path },
    });
    string written = Answer(ran).GetProperty("standardOutput").GetProperty("content").GetString()!;
    string result = written.Split('\n')[^1];
    Console.WriteLine($"run /{path}: {result}");
    if (!result.StartsWith('2') || !result.EndsWith(" 0"))
    {
        failed.Add(("/" + path, result));
    }
}

// The held request is still held when its client gives up; let the 3 s hold run out first.
await Task.Delay(TimeSpan.FromSeconds(3));

// The server's side: every exchange with its request line and the first line it sent back,
// and every fault_applied event with the exchange it acted on.
JsonElement exchanges = (await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server }))
    .StructuredContent!.Value.GetProperty("exchanges");
JsonElement applied = (await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
})).StructuredContent!.Value.GetProperty("events");
JsonElement rules = (await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server }))
    .StructuredContent!.Value.GetProperty("faults");

if (part == "rules")
{
    foreach (JsonElement rule in rules.EnumerateArray())
    {
        string id = rule.GetProperty("fault_id").GetString()!;
        int fired = applied.EnumerateArray().Count(e => e.GetProperty("rule_id").GetString() == id);
        Console.WriteLine($"{id} {rule.GetProperty("item").GetString()}: fired {fired}");
    }

    return 0;
}

if (part == "answers")
{
    // The server's side alone: each exchange it did not answer 2xx, and the faults applied to it.
    foreach (JsonElement exchange in exchanges.EnumerateArray())
    {
        long id = exchange.GetProperty("exchange_id").GetInt64();
        string answer = FirstLine(exchange, "sent");
        if (!answer.StartsWith("HTTP/1.1 2"))
        {
            Console.WriteLine($"exchange {id}: {FirstLine(exchange, "received")} -> {(answer == "" ? "nothing sent" : answer)}: {Faults(id)}");
        }
    }

    return 0;
}

// Each failed request of the run, matched to its exchange by request line (one exchange per
// call, in order), with what the server sent and the faults it applied there.
var used = new HashSet<long>();
foreach ((string path, string result) in failed)
{
    JsonElement exchange = exchanges.EnumerateArray().First(e =>
        !used.Contains(e.GetProperty("exchange_id").GetInt64()) && FirstLine(e, "received") == $"GET {path} HTTP/1.1");
    long id = exchange.GetProperty("exchange_id").GetInt64();
    used.Add(id);
    string answer = FirstLine(exchange, "sent");
    Console.WriteLine($"{path} ({result}): exchange {id}, sent {(answer == "" ? "nothing" : answer)}: {Faults(id)}");
}

return 0;

// The faults applied to one exchange, or a note that there were none.
string Faults(long id)
{
    var faults = applied.EnumerateArray()
        .Where(e => e.TryGetProperty("exchange_id", out JsonElement x) && x.ValueKind == JsonValueKind.Number && x.GetInt64() == id)
        .Select(e => $"{e.GetProperty("rule_id").GetString()} {e.GetProperty("fault_kind").GetString()}").ToList();
    return faults.Count == 0 ? "no fault, the server's own answer" : string.Join(", ", faults);
}

// The first line of what one side of an exchange carried, or "" if nothing.
static string FirstLine(JsonElement exchange, string direction) =>
    string.Concat(exchange.GetProperty("chunks").EnumerateArray()
        .Where(c => c.GetProperty("direction").GetString() == direction)
        .Select(c => c.GetProperty("data").GetString())).Split("\r\n")[0];

// curl-mcp answers in text, as JSON.
static JsonElement Answer(CallToolResult result) =>
    JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
