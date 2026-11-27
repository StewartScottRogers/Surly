// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a surl-mcp server that holds every request under /slow five seconds, then
// fetches a slow path and a fast one at once through curl-mcp.
// Run: dotnet run slow-path.cs -- [seconds] [path]
//   seconds   transfer's timeoutSeconds for each fetch (2 if not given; 0 for none)
//   path      the second path, fetched beside /slow/report.txt (/hello.txt if not given)
#:package ModelContextProtocol.Core@2.4.0

using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

double limit = args.Length > 0 ? double.Parse(args[0]) : 2;
string other = args.Length > 1 ? args[1] : "/hello.txt";

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

// The rule is surl's own --fault option, passed in arguments.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string>
    {
        ["hello.txt"] = "hello from surl\n", ["slow/report.txt"] = "the slow report\n", ["slowness.txt"] = "not slow at all\n",
    },
    ["arguments"] = new[] { "--fault", "delay;stage=command;path=/slow;ms=5000" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Two transfers at once; each prints a line as it ends.
await Task.WhenAll(Fetch("/slow/report.txt"), Fetch(other));

// The server kept an event each time the rule held a request.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
});
foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"get_events: {e.GetProperty("fault_kind")} on {e.GetProperty("target")}");
}

return 0;

// transfer answers in text, as JSON: a status, or a failure with curl's exit code.
async Task Fetch(string path)
{
    var request = new Dictionary<string, object?> { ["url"] = url + path.TrimStart('/') };
    if (limit > 0)
    {
        request["timeoutSeconds"] = limit;
    }

    var clock = Stopwatch.StartNew();
    CallToolResult result = await curl.CallToolAsync("transfer", request);
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement failure = answer.GetProperty("failure");
    string outcome = failure.ValueKind == JsonValueKind.Object
        ? $"curl exit {failure.GetProperty("exitCode")}, tool error: {(result.IsError == true ? "yes" : "no")}"
        : $"{answer.GetProperty("statusCode")}";
    Console.WriteLine($"{path}: {outcome}, {(clock.Elapsed.TotalSeconds < 5 ? "within" : "after")} 5 s");
}
