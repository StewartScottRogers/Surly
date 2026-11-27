// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reports unread counts with IMAP STATUS through curl-mcp and checks each against
// surl-mcp's list_mailboxes, as messages are read, marked unread, and new mail comes in.
// Run: dotnet run unread-counts.cs [-- <case>]
//   (no argument)  counts after each step, two ways
//   no-folder      STATUS for a folder that does not exist
//   no-size        STATUS with the IMAP4rev2 SIZE item
//   unknown-owner  list_mailboxes for an account that does not exist
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "counts";

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

// Three orders into ann's INBOX, each waited for, with a bound, by its delivery event.
for (int n = 1; n <= 3; n++)
{
    if (!await Seed(n))
    {
        Console.WriteLine($"message {n} never arrived");
        return 1;
    }
}

// STATUS refusals: a folder ann does not have (NO) and an IMAP4rev2 item (BAD); curl exits 21 either way.
if (which is "no-folder" or "no-size")
{
    string command = which == "no-folder" ? "STATUS Archive (MESSAGES UNSEEN)" : "STATUS INBOX (SIZE)";
    JsonElement failed = await Curl("-v", "-u", Ann, "-X", command, imap);
    string reply = failed.GetProperty("standardError").GetProperty("content").GetString()!
        .Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("< A003"));
    Console.WriteLine($"{command}: curl exit {failed.GetProperty("exitCode")}, {reply}");
    return 1;
}

if (which == "unknown-owner")
{
    CallToolResult missing = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "nobody@example.com" });
    Console.WriteLine($"list_mailboxes nobody: isError {missing.IsError}, {missing.Content.OfType<TextContentBlock>().Single().Text}");
    return 1;
}

await Report("before");

// A client reads message 1 over IMAP, which sets \Seen.
await Curl("-o", "message1.eml", "-u", Ann, imap + "INBOX;UID=1");
await Report("after curl read UID 1");

// A client reads only message 2's Subject: curl asks for BODY[...], not BODY.PEEK[...], so it is a read too.
Console.WriteLine($"UID 2 header: {Out(await Curl("-u", Ann, imap + "INBOX;UID=2;SECTION=HEADER.FIELDS%20(SUBJECT)"))}");
await Report("after curl read UID 2's header");

// STATUS counts; SEARCH names. read_message lets the agent read UID 3 itself without setting a flag.
Console.WriteLine($"SEARCH UNSEEN: {Out(await Curl("-u", Ann, imap + "INBOX?UNSEEN"))}");
CallToolResult peeked = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 3,
});
string subject = peeked.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n").Single(h => h.StartsWith("Subject: "));
Console.WriteLine($"read_message UID 3: {subject}");
await Report("after read_message UID 3");

// STORE -FLAGS (\Seen) makes message 1 unread again.
await Curl("-o", "store.txt", "-u", Ann, "-X", @"STORE 1 -FLAGS (\Seen)", imap + "INBOX");
await Report("after STORE 1 -FLAGS");

// UIDNEXT is the UID the next message will get; a fourth message, waited for by its event, raises both counts.
Console.WriteLine($"UIDNEXT: {Out(await Curl("-u", Ann, "-X", "STATUS INBOX (UIDNEXT)", imap))}");
if (!await Seed(4))
{
    Console.WriteLine("the fourth message never arrived");
    return 1;
}

await Report("after a fourth message");
return 0;

// seed_mail delivers one order; wait_for_event answers its store_changed event, or timed_out after 10 s.
async Task<bool> Seed(int n)
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "ann@example.com",
        ["message"] = $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {n} shipped\r\n\r\nOn its way.\r\n",
    });
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail",
        ["name"] = $"ann@example.com/INBOX/;UID={n}", ["timeout_ms"] = 10000,
    });
    return waited.StructuredContent!.Value.TryGetProperty("event", out _);
}

// The client's count: STATUS, which needs no SELECT. The server's: list_mailboxes, which sets no flag.
async Task Report(string when)
{
    JsonElement status = await Curl("-u", Ann, "-X", "STATUS INBOX (MESSAGES UNSEEN)", imap);
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
    JsonElement inbox = boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "INBOX");
    Console.WriteLine($"{when}: {Out(status)}; list_mailboxes {inbox.GetProperty("message_count")} messages, {inbox.GetProperty("unseen_count")} unseen");
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
