// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent finds out that a mailbox is IMAP-only: it tries POP3 through curl-mcp's curl tool, reads the
// refusal and what surl-mcp recorded, then switches to IMAP with the same login.
// Run: dotnet run imap-only.cs
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// SMTP, IMAP and POP3 on free ports over one mail store; ann logs in over IMAP alone, bob everywhere.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "pop3://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "-v", "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass;protocols=imap",
        "--mailbox", "bob@example.com:bobpass", "--mailbox", "shop@example.com:shoppass",
    },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;
string imap = server.GetProperty("bound_urls")[1].GetString()!;
string pop3 = server.GetProperty("bound_urls")[2].GetString()!;
long lastSequence = 0;

// The shop's mail to ann over SMTP; the agent waits, at most ten seconds, for its delivery.
await Curl("-u", "shop@example.com:shoppass", "--mail-from", "shop@example.com", "--mail-rcpt", "ann@example.com", "-T", "order.eml", smtp + "shop.example.com");
await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = "ann@example.com", ["timeout_ms"] = 10000,
});

// The agent's first try is POP3; refused, it switches to IMAP with the same login.
string tried = await Pop3("ann@example.com:annpass");
Console.WriteLine($"ann over POP3: {tried}");
if (tried.StartsWith("exitCode 67"))
{
    Console.WriteLine($"switched to IMAP: {Out(await Curl("-u", "ann@example.com:annpass", imap + "INBOX?ALL"))}");
}

Console.WriteLine($"nobody over POP3: {await Pop3("nobody@example.com:annpass")}");
Console.WriteLine($"bob, wrong password, over POP3: {await Pop3("bob@example.com:wrong")}");
Console.WriteLine($"ann over IMAP: {Out(await Curl("-u", "ann@example.com:annpass", imap + "INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)"))}");

// What the server judged: every POP3 login, alike.
CallToolResult judged = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" }, ["scheme"] = "pop3",
});
foreach (JsonElement e in judged.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"judged: {e.GetProperty("mechanism").GetString()} {e.GetProperty("account").GetString()} is_accepted {e.GetProperty("is_accepted").GetRawText()}");
}

// What the server said at start about each mailbox's logins.
Console.WriteLine($"log: {server.GetProperty("log_lines").EnumerateArray().Select(l => l.GetString()!).Single(l => l.StartsWith("* Mailbox ann@example.com:"))}");
return 0;

// A POP3 listing through the curl tool with -v; then a bounded wait until surl has ended the exchange.
async Task<string> Pop3(string login)
{
    JsonElement answer = await Curl("-v", "-u", login, pop3);
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "pop3",
        ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
    });
    lastSequence = waited.StructuredContent!.Value.GetProperty("event").GetProperty("sequence").GetInt64();
    string reply = answer.GetProperty("standardError").GetProperty("content").GetString()!
        .Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Contains("-ERR")) ?? "";
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!
        .Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("curl:")) ?? "";
    return $"exitCode {answer.GetProperty("exitCode")}, {error}, {reply}";
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
