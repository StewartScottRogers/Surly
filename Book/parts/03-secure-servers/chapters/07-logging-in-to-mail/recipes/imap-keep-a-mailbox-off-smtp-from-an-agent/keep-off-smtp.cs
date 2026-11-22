// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent keeps a test mailbox off SMTP: surl-mcp runs the server with the mailbox restricted by
// protocols=imap, curl-mcp's curl tool is the client on each protocol, and surl-mcp's events show
// what the server judged.
// Run: dotnet run keep-off-smtp.cs [-- <case>]
//   (no argument)  the viewer on IMAP, SMTP and POP3; app, with no restriction, on all three
//   wrong          the viewer's IMAP login with a wrong password
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "all";

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

// IMAP, SMTP and POP3 over one mail store; viewer is kept to IMAP, app has no protocols= list.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0", "smtp://127.0.0.1:0", "pop3://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "viewer@example.com:viewpass;protocols=imap", "--mailbox", "app@example.com:apppass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string imap = bound[0].GetString()!, smtp = bound[1].GetString()!, pop3 = bound[2].GetString()!;

if (which == "wrong")
{
    Console.WriteLine($"viewer, wrong password, IMAP: curl exit {await Exit("-u", "viewer@example.com:wrong", imap)}");
    return 0;
}

// app sends the viewer one message, then the agent waits up to ten seconds for the delivery event.
Console.WriteLine($"app sent over SMTP: curl exit {await Exit("--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", "viewer@example.com", "-T", "hi.eml", smtp + "example.com")}");
CallToolResult wait = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["timeout_ms"] = 10000,
});
JsonElement landed = wait.StructuredContent!.Value;
Console.WriteLine($"delivered: {(landed.TryGetProperty("event", out JsonElement e) ? e.GetProperty("name").GetString() : "no delivery event within 10 s")}");

// The viewer on each protocol, with the right password; the IMAP body is printed.
JsonElement read = await Curl("-u", "viewer@example.com:viewpass", imap + "INBOX;UID=1;SECTION=TEXT");
Console.WriteLine($"viewer over IMAP: curl exit {read.GetProperty("exitCode")}, {read.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}");
Console.WriteLine($"viewer over SMTP: curl exit {await Exit("--crlf", "-u", "viewer@example.com:viewpass", "--mail-from", "viewer@example.com", "--mail-rcpt", "app@example.com", "-T", "hi.eml", smtp + "example.com")}");
Console.WriteLine($"viewer over POP3: curl exit {await Exit("-u", "viewer@example.com:viewpass", pop3)}");

// A wrong password for app, an account that may use SMTP, and the reply every SMTP refusal got on the wire.
Console.WriteLine($"app, wrong password, over SMTP: curl exit {await Exit("--crlf", "-u", "app@example.com:wrong", "--mail-from", "app@example.com", "--mail-rcpt", "viewer@example.com", "-T", "hi.eml", smtp + "example.com")}");
await AllEndedAsync();
CallToolResult wire =await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
string[] replies = wire.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
    .Select(x => string.Concat(x.GetProperty("chunks").EnumerateArray().Where(c => c.GetProperty("direction").GetString() == "sent").Select(c => c.GetProperty("data").GetString())))
    .Select(text => text.Split("\r\n").FirstOrDefault(line => line.StartsWith("535")) ?? "").Where(line => line != "").ToArray();
Console.WriteLine($"SMTP refusals on the wire: {replies.Length}, all \"{replies.Distinct().Single()}\"");

// app on IMAP and POP3.
Console.WriteLine($"app over IMAP: curl exit {await Exit("-u", "app@example.com:apppass", imap)}");
Console.WriteLine($"app over POP3: curl exit {await Exit("-u", "app@example.com:apppass", pop3)}");

// The server's side: how it judged each of the viewer's logins, and what its mailbox holds.
CallToolResult logins = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement login in logins.StructuredContent!.Value.GetProperty("events").EnumerateArray()
    .Where(l => l.GetProperty("account").GetString() == "viewer@example.com"))
{
    Console.WriteLine($"login_judged: {login.GetProperty("mechanism")} {login.GetProperty("account")} is_accepted {login.GetProperty("is_accepted").GetRawText()}");
}

CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "viewer@example.com" });
JsonElement inbox = boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "INBOX");
Console.WriteLine($"list_mailboxes: INBOX holds {inbox.GetProperty("message_count")} message");
return 0;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// The server records an exchange's last bytes and its end a moment after curl has its answer: ask
// until every exchange that started has ended, for at most 10 s.
async Task AllEndedAsync()
{
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
    {
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "exchange_started", "exchange_ended" } });
        var kinds = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("kind").GetString()).ToList();
        if (kinds.Count(k => k == "exchange_started") == kinds.Count(k => k == "exchange_ended")) return;
    }
}

async Task<int> Exit(params string[] curlArguments) => (await Curl(curlArguments)).GetProperty("exitCode").GetInt32();
