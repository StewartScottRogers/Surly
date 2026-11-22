// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp server busy (503) for the first two requests for a report, fetches it
// through curl-mcp with retries, and asks the server what it saw. Run: dotnet run busy.cs -- <case>
//   curl       the curl tool with --retry 2 --retry-delay 1 (the default)
//   curl-few   the curl tool with -f --retry 1
//   health     the curl tool with --retry 2 on a path that is never busy
//   transfer   transfer in a loop, with the agent's own one-second wait
#:package ModelContextProtocol.Core@*

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "curl";

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

// A server on a free port with two files in memory, then a rule that makes /report.txt busy for two requests.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["report.txt"] = "report ready\n", ["health.txt"] = "up\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
{
    ["server_id"] = server, ["fault"] = "error;scheme=http;reply=503;path=/report.txt;times=2",
});
Console.WriteLine($"add_fault: {added.StructuredContent!.Value.GetProperty("fault_id")}");

// The first transfer curl-mcp makes has one-time set-up costs: make one, to a path that is never busy,
// before anything is timed.
await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = baseUrl + "health.txt" });

string url = baseUrl + (which == "health" ? "health.txt" : "report.txt");
var clock = Stopwatch.StartNew();
if (which is "curl" or "curl-few" or "health")
{
    // The curl tool runs a curl command line in-process and answers its exit code and output as JSON text.
    string[] retry = which switch
    {
        "curl" => ["--retry", "2", "--retry-delay", "1"],
        "curl-few" => ["-f", "--retry", "1", "--retry-delay", "1"],
        _ => ["--retry", "2"],
    };
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "--no-progress-meter", "-w", "%{http_code} %{num_retries}\n" }.Concat(retry).Append(url).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"curl: exit {answer.GetProperty("exitCode")}, tool error: {(ran.IsError == true ? "yes" : "no")}");
    foreach (string line in Lines(answer, "standardOutput").Concat(Lines(answer, "standardError")))
    {
        Console.WriteLine($"  {line}");
    }
}
else
{
    // transfer makes one attempt each call; a 503 is an answer, so the agent decides to wait and ask again.
    for (int attempt = 1; attempt <= 4; attempt++)
    {
        CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
        JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        int status = answer.GetProperty("statusCode").GetInt32();
        Console.Write($"transfer {attempt}: {status}, failure {answer.GetProperty("failure").ValueKind.ToString().ToLowerInvariant()}, tool error: {(fetched.IsError == true ? "yes" : "no")}");
        if (status != 503)
        {
            Console.WriteLine($", {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}");
            break;
        }

        Console.WriteLine(", waiting 1 s");
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}

// Timing as a range that holds on a loaded machine: two waits of one second, nowhere near twenty.
if (which is "curl" or "transfer")
{
    Console.WriteLine($"waited between 2 and 20 s: {clock.Elapsed.TotalSeconds is >= 2 and < 20}");
}

// The server records an exchange a moment after curl has its answer: ask until every exchange
// that started has ended, for at most 10 s, before reading the record.
for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
{
    JsonElement seen = (await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = server, ["kinds"] = new[] { "exchange_started", "exchange_ended" } })).StructuredContent!.Value;
    var kinds = seen.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("kind").GetString()).ToList();
    if (kinds.Count(k => k == "exchange_started") == kinds.Count(k => k == "exchange_ended")) break;
}

// What the server saw: the request lines it received for the path, the times the rule acted, what is left of it.
CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
string wire = string.Concat(exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
    .SelectMany(e => e.GetProperty("chunks").EnumerateArray())
    .Where(c => c.GetProperty("direction").GetString() == "received")
    .Select(c => c.GetProperty("data").GetString()));
string path = which == "health" ? "health.txt" : "report.txt";
Console.WriteLine($"get_exchanges: {Regex.Count(wire, $"GET /{path} HTTP/1.1")} requests for /{path}");
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
});
Console.WriteLine($"get_events: {events.StructuredContent!.Value.GetProperty("events").GetArrayLength()} fault_applied");
CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server });
JsonElement rule = faults.StructuredContent!.Value.GetProperty("faults")[0];
Console.WriteLine($"list_faults: {rule.GetProperty("fault_id")} remaining {rule.GetProperty("remaining")}");
return 0;

// One stream of the curl tool's answer, line by line, without line ends.
static IEnumerable<string> Lines(JsonElement answer, string stream) =>
    (answer.GetProperty(stream).GetProperty("content").GetString() ?? "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r'));
