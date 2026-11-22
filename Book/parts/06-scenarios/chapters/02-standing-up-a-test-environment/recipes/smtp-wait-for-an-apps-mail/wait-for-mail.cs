// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent triggers an app that mails a little later, then waits on surl-mcp's wait_for_event for the
// delivery instead of polling the mailbox. The app is played by a task that sleeps, then sends with
// curl-mcp's curl tool.
// Run: dotnet run wait-for-mail.cs -- <case>
//   right      the app mails ann after 2 s; the agent waits at most 10 s (the default)
//   too-late   the app mails ann after 4 s; the agent waits at most 1 s, then waits again
//   elsewhere  the app mails bob instead; the agent waits at most 3 s for mail to ann
//   already    the app has mailed ann before the agent starts to wait
#:package ModelContextProtocol.Core@*

using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "right";

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

// SMTP for the app, example.com, three mailboxes.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The agent triggers the app, which mails some time later.
(int delayMs, string to, int timeoutMs) = which switch
{
    "too-late" => (4000, "ann@example.com", 1000),
    "elsewhere" => (0, "bob@example.com", 3000),
    "already" => (0, "ann@example.com", 10000),
    _ => (2000, "ann@example.com", 10000),
};
Task app = App(delayMs, to);
if (which == "already")
{
    await app;
}

// Wait for the mail store to deliver to ann, at most timeoutMs.
var clock = Stopwatch.StartNew();
JsonElement waited = await WaitForMail(timeoutMs);
long ms = clock.ElapsedMilliseconds;
if (waited.TryGetProperty("timed_out", out _))
{
    Console.WriteLine($"wait_for_event: {waited.GetRawText()}, after at least {timeoutMs} ms: {ms >= timeoutMs}");
    await app;
    if (which == "too-late")
    {
        // The event is kept, so a second wait returns it, whenever it is called.
        JsonElement late = await WaitForMail(10000);
        Console.WriteLine($"second wait: {late.GetProperty("event").GetProperty("name")}");
    }

    return 1;
}

JsonElement e = waited.GetProperty("event");
Console.WriteLine($"wait_for_event: {e.GetProperty("kind")} {e.GetProperty("change")} {e.GetProperty("name")}");
Console.WriteLine(which == "already" ? $"returned at once: {ms < 1000}" : $"returned once the app had sent, before the timeout: {ms >= delayMs && ms < timeoutMs}");
await app;
return 0;

async Task<JsonElement> WaitForMail(int timeout)
{
    CallToolResult result = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = "ann@example.com", ["timeout_ms"] = timeout,
    });
    return result.StructuredContent!.Value;
}

// The app under test: after a delay, one mail sent with the curl tool.
async Task App(int delay, string recipient)
{
    await Task.Delay(delay);
    await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", recipient, "-T", "receipt.eml", smtp + "example.com" },
    });
}
