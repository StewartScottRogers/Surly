// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side: an app sends Ann a welcome mail, Ann's mail client shows no new mail, and the
// agent reads the server's mail store and record to find where the mail went.
// Run: dotnet run find-the-mail.cs [-- typo | discarded | seen]
//   typo (default)  the app mistyped the address, anne@; a catch-all keeps unknown addresses
//   discarded       the same mistake on a server with no catch-all
//   seen            the address is right, but another device read the mail first
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "typo";
string recipient = scenario == "seen" ? "ann@example.com" : "anne@example.com";

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

JsonElement Structured(CallToolResult r) => r.StructuredContent!.Value;
JsonElement Text(CallToolResult r) => JsonDocument.Parse(r.Content.OfType<TextContentBlock>().Single().Text).RootElement;

// The mail server: SMTP and IMAP over one store, for example.com, with mailboxes for Ann, the app
// and the postmaster; in the typo scenario the postmaster also catches every unknown address.
var arguments = new List<string> { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpw",
    "--mailbox", "app@example.com:apppw", "--mailbox", "postmaster@example.com:pmpw" };
if (scenario == "typo") arguments.AddRange(["--mail-catch-all", "example.com=postmaster@example.com"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string serverId = Structured(started).GetProperty("server_id").GetString()!;
string smtp = Structured(started).GetProperty("bound_urls")[0].GetString()!;
string imap = Structured(started).GetProperty("bound_urls")[1].GetString()!;

// The app sends the welcome mail, logged in as itself, to the address it was given.
async Task<JsonElement> Curl(params string[] argv) => Text(await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv }));
JsonElement sent = await Curl("-s", "-u", "app@example.com:apppw", "--mail-from", "app@example.com", "--mail-rcpt", recipient, "-T", "welcome.eml", $"{smtp}app");
Console.WriteLine($"app: sent to {recipient}, exit {sent.GetProperty("exitCode").GetInt32()}");

// Wait, at most 10 s, for the SMTP exchange to end: the server has done all it will with the mail.
CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = serverId, ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp", ["timeout_ms"] = 10000,
});
if (Structured(ended).TryGetProperty("timed_out", out _)) { Console.WriteLine("the SMTP exchange did not end within 10 s"); return 1; }

// Another device of Ann's reads the mail first, in the seen scenario.
if (scenario == "seen") await Curl("-s", "-o", "phone.eml", "-u", "ann@example.com:annpw", $"{imap}INBOX;UID=1");

// Ann's mail client asks for the unread mail in her INBOX, and finds none.
JsonElement unread = await Curl("-s", "-u", "ann@example.com:annpw", $"{imap}INBOX?UNSEEN");
Console.WriteLine($"client: {unread.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}");

// The agent: first, what Ann's INBOX holds.
CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = serverId, ["owner"] = "ann@example.com" });
JsonElement inbox = Structured(boxes).GetProperty("mailboxes").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "INBOX");
Console.WriteLine($"ann@example.com INBOX: {inbox.GetProperty("message_count").GetInt32()} messages, {inbox.GetProperty("unseen_count").GetInt32()} unseen");

// Mail that is there but read: its flags say so.
if (inbox.GetProperty("message_count").GetInt32() > 0)
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?> { ["server_id"] = serverId, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX" });
    foreach (JsonElement m in Structured(listed).GetProperty("messages").EnumerateArray())
    {
        Console.WriteLine($"  UID {m.GetProperty("uid").GetInt64()}: flags {string.Join(" ", m.GetProperty("flags").EnumerateArray().Select(f => f.GetString()))}");
    }
}

// Where the mail went: a store_changed event for each mailbox a delivery reached, and for each
// later change to a message, such as a flag set when a client read it.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = serverId, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail" });
var deliveries = Structured(events).GetProperty("events").EnumerateArray().ToList();
Console.WriteLine(deliveries.Count == 0 ? "mail store: no change" : $"mail store: {string.Join(", ", deliveries.Select(e => e.GetProperty("change").GetString() + " " + e.GetProperty("name").GetString()))}");

// What the app asked for: the RCPT command in the SMTP exchange and the reply after it.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId, ["scheme"] = "smtp" });
var chunks = Structured(recorded).GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray().ToList();
int rcpt = chunks.FindIndex(c => c.GetProperty("direction").GetString() == "received" && c.GetProperty("data").GetString()!.StartsWith("RCPT TO:", StringComparison.Ordinal));
Console.WriteLine($"smtp: {chunks[rcpt].GetProperty("data").GetString()!.TrimEnd()} answered {chunks[rcpt + 1].GetProperty("data").GetString()!.TrimEnd()}");

// The caught copy still says To: ann@: the headers were right, the envelope was not.
if (deliveries.Count > 0 && scenario == "typo")
{
    CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?> { ["server_id"] = serverId, ["owner"] = "postmaster@example.com", ["mailbox"] = "INBOX", ["uid"] = 1 });
    string text = Structured(read).GetProperty("text").GetString()!;
    Console.WriteLine($"caught copy: {text.Split("\r\n").First(l => l.StartsWith("To: ", StringComparison.Ordinal))}");
}
return 0;
