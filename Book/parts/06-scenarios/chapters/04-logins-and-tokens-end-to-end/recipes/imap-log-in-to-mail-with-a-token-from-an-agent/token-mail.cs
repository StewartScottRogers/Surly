// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a mail flow with an OAuth 2.0 token, end to end: surl-mcp runs a server that accepts only
// OAUTHBEARER and XOAUTH2, curl-mcp's curl tool sends and reads mail with the token and tries every way in that
// should stay shut, and surl-mcp shows what the server stored and judged.
// Run: dotnet run token-mail.cs [-- why]   (why: ordinary mailboxes, to show what protocols=http closes)
// server.pem and server.key, a certificate and key for 127.0.0.1, are in this folder.
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

// With the argument "why" the mailboxes are ordinary ones, to show what protocols=http closes.
string closed = args.Length > 0 && args[0] == "why" ? "" : ";protocols=http";

// smtps and imaps over TLS, smtp and imap without, over one mail store. --auth names the two token mechanisms;
// the mailboxes take mail but log in on no mail protocol (protocols=http), so a password is no way in.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtps://127.0.0.1:0", "imaps://127.0.0.1:0", "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--cert", "server.pem", "--key", "server.key", "--auth", "oauthbearer,xoauth2", "--user", ":tok-abc123",
        "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" + closed, "--mailbox", "bob@example.com:bobpass" + closed,
    },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string smtps = bound[0].GetString()!, imaps = bound[1].GetString()!, smtp = bound[2].GetString()!, imap = bound[3].GetString()!;

// One curl run: the exit code, and the output when it worked. -sS keeps the progress meter out and the error message in.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-sS").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// A send of lunch.eml as bob, with the token or a password; a read of ann's message 1 the same way.
async Task<string> Send(string url, string[] login, string to = "ann@example.com")
{
    JsonElement r = await Curl(["--cacert", "server.pem", "--crlf", .. login, "--mail-from", "bob@example.com", "--mail-rcpt", to, "-T", "lunch.eml", "-w", "%{response_code}", url + "example.com"]);
    int exit = r.GetProperty("exitCode").GetInt32();
    return exit == 0 ? $"SMTP {r.GetProperty("standardOutput").GetProperty("content").GetString()}" : $"curl exit {exit}: {r.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}";
}

async Task<string> Read(string url, string[] login)
{
    JsonElement r = await Curl(["--cacert", "server.pem", .. login, url + "INBOX;UID=1;SECTION=TEXT"]);
    int exit = r.GetProperty("exitCode").GetInt32();
    return exit == 0 ? r.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim() : $"curl exit {exit}";
}

// Waits, for at most ten seconds, for the first delivery after the given event; returns its sequence and name.
async Task<(long Sequence, string Name)> Delivery(long after)
{
    CallToolResult wait = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["after_sequence"] = after, ["timeout_ms"] = 10000,
    });
    return wait.StructuredContent!.Value.TryGetProperty("event", out JsonElement e)
        ? (e.GetProperty("sequence").GetInt64(), e.GetProperty("name").GetString()!)
        : (after, "no delivery event within 10 s");
}

if (closed == "")
{
    JsonElement listing = await Curl("--cacert", "server.pem", "-u", "ann@example.com:annpass", imaps);
    Console.WriteLine($"ann's password over imaps, no protocols=http: curl exit {listing.GetProperty("exitCode")}, {listing.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}");
    return 0;
}

string[] token(string user) => ["-u", user, "--oauth2-bearer", "tok-abc123"];

// The happy path: bob sends with the token, the agent waits for the delivery, ann reads.
Console.WriteLine($"bob sends over smtps with the token: {await Send(smtps, token("bob@example.com"))}");
(long first, string name) = await Delivery(0);
Console.WriteLine($"delivered: {name}");
Console.WriteLine($"ann reads over imaps with the token: {await Read(imaps, token("ann@example.com"))}");

// What the server stored: the message as it holds it.
CallToolResult stored = await surl.CallToolAsync("read_message", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 1 });
string text = stored.StructuredContent!.Value.GetProperty("text").GetString()!;
Console.WriteLine($"stored with ESMTPSA, from bob: {text.Contains("with ESMTPSA;") && text.StartsWith("Return-Path: <bob@example.com>")}");

// XOAUTH2 on request, then every way in that stays shut.
Console.WriteLine($"bob sends with XOAUTH2: {await Send(smtps, [.. token("bob@example.com"), "--login-options", "AUTH=XOAUTH2"])}");
await Delivery(first);
Console.WriteLine($"bob's password over smtps: {(await Send(smtps, ["-u", "bob@example.com:bobpass"])).Split(':')[0]}");
Console.WriteLine($"ann's password over imaps: {await Read(imaps, ["-u", "ann@example.com:annpass"])}");
Console.WriteLine($"wrong token over smtps: {(await Send(smtps, ["-u", "bob@example.com", "--oauth2-bearer", "wrong-token"])).Split(':')[0]}");
Console.WriteLine($"token over plain smtp: {await Send(smtp, token("bob@example.com"))}");
Console.WriteLine($"token over plain imap: {await Read(imap, token("ann@example.com"))}");
Console.WriteLine($"token to a recipient outside the domain: {await Send(smtps, token("bob@example.com"), "carol@elsewhere.org")}");

// What the server recorded: how many messages are in ann's INBOX, and every login it judged.
CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
Console.WriteLine($"list_mailboxes: INBOX holds {boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "INBOX").GetProperty("message_count")} messages");
CallToolResult logins = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "login_judged" } });
foreach (JsonElement login in logins.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"login_judged: {login.GetProperty("scheme").GetString()} {login.GetProperty("mechanism").GetString()} {login.GetProperty("account").GetString()} is_accepted {login.GetProperty("is_accepted").GetRawText()}");
}

return 0;
