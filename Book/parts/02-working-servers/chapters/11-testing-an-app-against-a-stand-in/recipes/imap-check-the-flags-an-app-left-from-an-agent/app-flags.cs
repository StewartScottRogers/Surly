// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks the flags a mail client left: the app reads two orders and marks the spam
// \Deleted through curl-mcp's curl tool; the agent then reads every message's flags with
// surl-mcp's list_messages.
// Run: dotnet run app-flags.cs
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

// IMAP on a free port, example.com, one mailbox.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
const string Ann = "ann@example.com:annpass";

// Four messages into ann's INBOX, each waited for, with a bound, by its delivery event.
string[] subjects = ["Order 1 shipped", "Order 2 shipped", "You have won a prize", "Our autumn newsletter"];
for (int uid = 1; uid <= subjects.Length; uid++)
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "ann@example.com",
        ["message"] = $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subjects[uid - 1]}\r\n\r\nSee inside.\r\n",
    });
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail",
        ["name"] = $"ann@example.com/INBOX/;UID={uid}", ["timeout_ms"] = 10000,
    });
    if (!waited.StructuredContent!.Value.TryGetProperty("event", out _))
    {
        Console.WriteLine($"message {uid} never arrived");
        return 1;
    }
}

// The app's run: it reads both orders, reads the spam's subject and marks it \Deleted, and leaves the newsletter alone.
await Curl("-o", "read1.eml", "-u", Ann, imap + "INBOX;UID=1");
await Curl("-o", "read2.eml", "-u", Ann, imap + "INBOX;UID=2");
await Curl("-o", "subject3.txt", "-u", Ann, imap + "INBOX;UID=3;SECTION=HEADER.FIELDS%20(SUBJECT)");
await Curl("-o", "store3.txt", "-u", Ann, "-X", @"STORE 3 +FLAGS (\Deleted)", imap + "INBOX");

// The check: every message's flags from list_messages, which sets none.
Console.WriteLine($"after the app: {await Flags()}");
Console.WriteLine($"listed again: {await Flags()}");
Console.WriteLine($"IMAP UNSEEN: {Out(await Curl("-u", Ann, imap + "INBOX?UNSEEN"))}");
Console.WriteLine($"IMAP DELETED: {Out(await Curl("-u", Ann, imap + "INBOX?DELETED"))}");
Console.WriteLine($"IMAP SEEN NOT DELETED: {Out(await Curl("-u", Ann, imap + "INBOX?SEEN%20NOT%20DELETED"))}");
CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
JsonElement inbox = boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "INBOX");
Console.WriteLine($"list_mailboxes: {inbox.GetProperty("message_count")} messages, {inbox.GetProperty("unseen_count")} unseen");

// read_message lets the agent look inside without a flag; fetching over IMAP to look is a read and sets \Seen.
CallToolResult peeked = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 4,
});
string subject = peeked.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n").Single(h => h.StartsWith("Subject: "));
Console.WriteLine($"read_message UID 4: {subject}; after it: {await Flags()}");
await Curl("-o", "read4.eml", "-u", Ann, imap + "INBOX;UID=4");
Console.WriteLine($"after a curl fetch of UID 4: {await Flags()}");
Console.WriteLine($"STORE 4 -FLAGS (\\Seen): {Out(await Curl("-u", Ann, "-X", @"STORE 4 -FLAGS (\Seen)", imap + "INBOX"))}");

// EXPUNGE removes what is marked \Deleted; numbers shift, UIDs do not.
Console.WriteLine($"EXPUNGE: {Out(await Curl("-u", Ann, "-X", "EXPUNGE", imap + "INBOX"))}");
Console.WriteLine($"after EXPUNGE: {await Flags()}");
Console.WriteLine($"IMAP UID FETCH: {Out(await Curl("-u", Ann, "-X", "UID FETCH 1:* FLAGS", imap + "INBOX")).ReplaceLineEndings(" | ")}");
return 0;

// list_messages answers each message's uid and flags, in UID order.
async Task<string> Flags()
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
    });
    return string.Join("; ", listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray().Select(m =>
        $"UID {m.GetProperty("uid")} ({string.Join(" ", m.GetProperty("flags").EnumerateArray().Select(f => f.GetString()))})"));
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
