// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp server drop the first request for a file, fetches it through curl-mcp,
// and counts what each side saw. Run: dotnet run recover.cs -- <case>
//   retry      the curl tool with --retry 1 --retry-all-errors (the default)
//   retry-alone  the curl tool with --retry 1 only
//   transfer   transfer twice: it makes no retry of its own
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "retry";

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

// A server on a free port with one file in memory, then a rule that drops the first request for it.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";
CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
{
    ["server_id"] = server, ["fault"] = "drop;stage=command;path=/hello.txt;times=1",
});
Console.WriteLine($"add_fault: {added.StructuredContent!.Value.GetProperty("fault_id")}");

if (which is "retry" or "retry-alone")
{
    // The curl tool runs a curl command line in-process and answers its exit code and output as JSON text.
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = which == "retry"
            ? new[] { "-s", "--retry", "1", "--retry-all-errors", "-w", "%{num_retries}\n", url }
            : new[] { "-s", "--retry", "1", "-w", "%{num_retries}\n", url },
    });
    JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    Console.WriteLine($"curl: exit {answer.GetProperty("exitCode")}, {output.Replace("\n", " | ").TrimEnd(' ', '|')}");
}
else
{
    // transfer makes one attempt each call; a dropped connection is a failure with curl's exit code.
    for (int attempt = 1; attempt <= 2; attempt++)
    {
        CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
        JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        JsonElement failure = answer.GetProperty("failure");
        Console.WriteLine(failure.ValueKind == JsonValueKind.Object
            ? $"transfer {attempt}: curl exit {failure.GetProperty("exitCode")}, tool error: {(fetched.IsError == true ? "yes" : "no")}"
            : $"transfer {attempt}: {answer.GetProperty("statusCode")} {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}");
    }
}

// The server records an exchange a moment after curl has its answer: ask until every exchange
// that started has ended, for at most 10 s, before reading the record.
for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
{
    JsonElement seen = (await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = server, ["kinds"] = new[] { "exchange_started", "exchange_ended" } })).StructuredContent!.Value;
    var kinds = seen.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("kind").GetString()).ToList();
    if (kinds.Count(k => k == "exchange_started") == kinds.Count(k => k == "exchange_ended")) break;
}

// What the server counted: its exchanges, the times the rule acted, and what is left of the rule.
CallToolResult servers = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
JsonElement mine = servers.StructuredContent!.Value.GetProperty("servers").EnumerateArray()
    .Single(s => s.GetProperty("server_id").GetString() == server);
Console.WriteLine($"list_servers: exchange_count {mine.GetProperty("exchange_count")}");
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
});
Console.WriteLine($"get_events: {events.StructuredContent!.Value.GetProperty("events").GetArrayLength()} fault_applied");
CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server });
JsonElement rule = faults.StructuredContent!.Value.GetProperty("faults")[0];
Console.WriteLine($"list_faults: {rule.GetProperty("fault_id")} remaining {rule.GetProperty("remaining")}");
return 0;
