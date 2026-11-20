// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp server answer the first two requests for a file 503, fetches it
// through curl-mcp with retries, and counts what the server did. Run: dotnet run flaky.cs -- <case>
//   curl       the curl tool with --retry 3 (the default)
//   curl-few   the curl tool with -f --retry 1
//   transfer   transfer in a loop, with the agent's own doubling wait
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
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

// A server on a free port with one file in memory, then a rule that answers the first two requests for it 503.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";
CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
{
    ["server_id"] = server, ["fault"] = "error;scheme=http;reply=503;path=/hello.txt;times=2",
});
Console.WriteLine($"add_fault: {added.StructuredContent!.Value.GetProperty("fault_id")}");

if (which is "curl" or "curl-few")
{
    // The curl tool runs a curl command line in-process and answers its exit code and output as JSON text.
    string[] retry = which == "curl" ? ["--retry", "3"] : ["-f", "--retry", "1"];
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
    TimeSpan wait = TimeSpan.FromSeconds(1);
    for (int attempt = 1; attempt <= 4; attempt++)
    {
        CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
        JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        int status = answer.GetProperty("statusCode").GetInt32();
        Console.Write($"transfer {attempt}: {status}, failure {answer.GetProperty("failure").ValueKind.ToString().ToLowerInvariant()}, tool error: {(fetched.IsError == true ? "yes" : "no")}");
        if (status != 503)
        {
            Console.Write($", {answer.GetProperty("body").GetProperty("content").GetString()}");
            break;
        }

        Console.WriteLine($", waiting {wait.TotalSeconds} s");
        await Task.Delay(wait);
        wait *= 2;
    }
}

// What the server counted: the times the rule acted, and what is left of it.
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
