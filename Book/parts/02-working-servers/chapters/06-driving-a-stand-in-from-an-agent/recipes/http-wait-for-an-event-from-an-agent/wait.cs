// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent waits on a running surl server's events through surl-mcp while curl, through
// curl-mcp, uploads a report to it. Run: dotnet run wait.cs -- <case>
//   solution              upload, then wait for the change under /uploads
//   wait-first            wait for the change under /uploads, and upload while it waits
//   nothing-yet           wait for a change under /reports, which nothing makes
//   whole-segments        upload, then wait for a change under /upload
//   by-kind               upload, then wait for the login_judged event
//   the-whole-story       upload, then get_events
//   a-page-at-a-time      upload, then get_events with max_events 1
//   timeout-bounds        wait_for_event with timeout_ms 70000
//   no-password-in-clear  the upload with Basic authentication instead of Digest
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
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

// An HTTP server that takes uploads from alice, with an uploads folder to write into.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-uploads", "--user", "alice:secret" },
    ["files"] = new Dictionary<string, string> { ["uploads/README.txt"] = "Reports go here.\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "solution":
        await Upload("--digest");
        Console.WriteLine(await Wait(new() { ["store"] = "content", ["name"] = "/uploads", ["timeout_ms"] = 5000 }));
        break;
    case "wait-first":
        // The wait is sent first and answers when the upload's write happens.
        Task<string> waiting = Wait(new() { ["store"] = "content", ["name"] = "/uploads", ["timeout_ms"] = 5000 });
        await Upload("--digest");
        Console.WriteLine(await waiting);
        break;
    case "nothing-yet":
        Console.WriteLine(await Wait(new() { ["store"] = "content", ["name"] = "/reports", ["timeout_ms"] = 200 }));
        break;
    case "whole-segments":
        await Upload("--digest");
        Console.WriteLine(await Wait(new() { ["store"] = "content", ["name"] = "/upload", ["timeout_ms"] = 200 }));
        break;
    case "by-kind":
        await Upload("--digest");
        Console.WriteLine(await Wait(new() { ["kinds"] = new[] { "login_judged" }, ["timeout_ms"] = 5000 }));
        break;
    case "the-whole-story":
        await Upload("--digest");
        await GetEvents(new());
        break;
    case "a-page-at-a-time":
        await Upload("--digest");
        await GetEvents(new() { ["max_events"] = 1 });
        break;
    case "timeout-bounds":
        Console.WriteLine(await Wait(new() { ["timeout_ms"] = 70000 }));
        break;
    case "no-password-in-clear":
        await Upload();
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// The curl tool runs curl's own arguments: -T uploads report.txt, a file in this folder, with a PUT.
async Task Upload(params string[] how)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s" }.Concat(how).Concat(new[] { "-u", "alice:secret", "-T", "report.txt", "-w", "%{http_code}", url + "uploads/report.txt" }).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    Console.WriteLine($"curl exit {answer.RootElement.GetProperty("exitCode")}, status {answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()}");
}

// wait_for_event answers the first event that matches, or timed_out true when none comes in time.
async Task<string> Wait(Dictionary<string, object?> filters)
{
    filters["server_id"] = server;
    CallToolResult result = await surl.CallToolAsync("wait_for_event", filters);
    if (result.IsError == true)
    {
        return $"wait_for_event: error: {result.Content.OfType<TextContentBlock>().Single().Text}";
    }

    JsonElement answer = result.StructuredContent!.Value;
    return answer.TryGetProperty("event", out JsonElement e) ? "wait_for_event: " + Describe(e) : $"wait_for_event: {answer.GetRawText()}";
}

// get_events answers the events kept, oldest first, a page at a time.
async Task GetEvents(Dictionary<string, object?> filters)
{
    filters["server_id"] = server;
    CallToolResult result = await surl.CallToolAsync("get_events", filters);
    foreach (JsonElement e in result.StructuredContent!.Value.GetProperty("events").EnumerateArray())
    {
        Console.WriteLine("get_events: " + Describe(e));
    }

    Console.WriteLine($"get_events: has_more {result.StructuredContent!.Value.GetProperty("has_more").GetRawText()}");
}

// An event's fields, leaving out those that differ from run to run: its sequence number, its time,
// the client's port and the byte counts. listen_url is shown as "url" when it is the server's own.
string Describe(JsonElement e) => string.Join(" ", e.EnumerateObject()
    .Where(p => p.Name is not ("sequence" or "at" or "client" or "received_byte_count" or "sent_byte_count"))
    .Select(p => p.Name == "listen_url" && p.Value.GetString() == url ? "listen_url=url" : $"{p.Name}={Text(p.Value)}"));

string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
