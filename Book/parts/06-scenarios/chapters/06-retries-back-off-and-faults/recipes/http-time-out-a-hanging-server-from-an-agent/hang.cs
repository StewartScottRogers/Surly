// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp server hold the first request for a file five seconds, fetches it
// through curl-mcp under a time limit, and asks the server what it held.
// Run: dotnet run hang.cs -- <case>
//   timeout    transfer with timeoutSeconds 2 (the default)
//   none       transfer with no limit
//   retry      the curl tool with -m 2 --retry 1
#:package ModelContextProtocol.Core@2.4.0

using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "timeout";

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

// The rule is surl's own --fault option, passed in arguments: hold the first request for /hello.txt 5 s.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    ["arguments"] = new[] { "--fault", "delay;ms=5000;stage=command;path=/hello.txt;times=1" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

var clock = Stopwatch.StartNew();
CallToolResult result;
if (which == "retry")
{
    // The curl tool runs a curl command line in-process; -m limits each try and --retry retries a timeout.
    result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "--no-progress-meter", "-m", "2", "--retry", "1", "-w", "%{http_code} %{num_retries}\n", url },
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"curl: exit {answer.GetProperty("exitCode")}");
    foreach (string stream in new[] { "standardOutput", "standardError" })
    {
        foreach (string line in (answer.GetProperty(stream).GetProperty("content").GetString() ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Console.WriteLine($"  {line.TrimEnd('\r')}");
        }
    }
}
else
{
    // transfer: one attempt, with timeoutSeconds as the limit on the whole transfer, or none.
    var request = new Dictionary<string, object?> { ["url"] = url };
    if (which == "timeout")
    {
        request["timeoutSeconds"] = 2;
    }

    result = await curl.CallToolAsync("transfer", request);
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement failure = answer.GetProperty("failure");
    Console.WriteLine(failure.ValueKind == JsonValueKind.Object
        ? $"transfer: curl exit {failure.GetProperty("exitCode")}, {failure.GetProperty("kind")}"
        : $"transfer: {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}");
}

Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}, {(clock.Elapsed.TotalSeconds < 5 ? "within" : "after")} 5 s");

// What the server kept: an event each time the rule held a request, and what is left of the rule.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
});
foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"get_events: {e.GetProperty("fault_kind")} on {e.GetProperty("target")}");
}

CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server });
JsonElement rule = faults.StructuredContent!.Value.GetProperty("faults")[0];
Console.WriteLine($"list_faults: {rule.GetProperty("fault_id")} remaining {rule.GetProperty("remaining")}");
return result.IsError == true ? 1 : 0;
