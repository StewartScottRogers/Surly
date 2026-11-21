// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent retrieves a seeded message over POP3, deletes it and confirms with list_messages that it is
// gone: surl-mcp starts the mail server and seeds two messages, curl-mcp's curl tool is the POP3 client,
// and surl-mcp's tools read the store the client changed. There is no delete tool: a POP3 client deletes.
// Run: dotnet run retrieve-and-delete.cs -- <case>
//   delete     retrieve message 1, delete it, and list what the store holds
//   list       the POP3 listing, and the store's, before anything is deleted
//   retrieve   the POP3 text of message 1 against read_message's, and the store afterwards
//   renumber   after the deletion POP3 numbers the survivor 1, while its UID stays 2
//   rset       DELE then RSET in one session: nothing is deleted
//   missing    DELE of a message number that is not there
//   events     the event the deletion makes, and the mailbox counts
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "delete";

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

// POP3 and IMAP over one mail store, for ann.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "pop3://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string pop3 = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

// Two messages are seeded; each is waited for by its delivery event, with a bound.
long seen = 0;
string[] subjects = ["Lunch", "Invoice 42"];
foreach (string subject in subjects)
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id,
        ["to"] = "ann@example.com",
        ["message"] = $"From: bob@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nAbout {subject}.\r\n",
    });
    seen = await NextEvent(seen);
    if (seen == 0)
    {
        Console.WriteLine($"{subject} did not arrive within 10 s");
        return 1;
    }
}

string who = "ann@example.com:annpass";
switch (mode)
{
    case "list":
        string[] octets = (await Curl("-u", who, pop3)).Lines;
        Console.WriteLine($"POP3 LIST: {string.Join(" | ", octets.Select(Shape))}");
        await Store();
        CallToolResult sizes = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
        });
        Console.WriteLine($"octets agree with list_messages sizes: {octets.Select(l => int.Parse(l.Split(' ')[1])).SequenceEqual(sizes.StructuredContent!.Value.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("size").GetInt32()))}");
        break;
    case "retrieve":
        string retrieved = (await Curl("-u", who, pop3 + "1")).Out;
        CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 1,
        });
        Console.WriteLine($"RETR 1: {retrieved.Replace("\r\n", "/")}");
        Console.WriteLine($"same as read_message: {retrieved == read.StructuredContent!.Value.GetProperty("text").GetString()}");
        await Store();
        break;
    case "renumber":
        await Delete1();
        Console.WriteLine($"POP3 LIST: {string.Join(" | ", (await Curl("-u", who, pop3)).Lines.Select(Shape))}");
        await Store();
        break;
    case "rset":
        Reply session = await Curl("-X", "DELE 1", "-u", who, pop3, "--next", "-u", who, pop3, "--next", "-X", "RSET", "-u", who, pop3);
        Console.WriteLine($"DELE 1, LIST, RSET in one session: curl exit {session.Exit}");
        await Store();
        break;
    case "missing":
        Reply missing = await Curl("-v", "-X", "DELE 5", "-u", who, pop3);
        Console.WriteLine($"DELE 5: curl exit {missing.Exit}, {missing.Err.Split('\n').First(line => line.Contains("-ERR")).Trim()}");
        await Store();
        break;
    case "events":
        long before = seen;
        await Delete1();
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["after_sequence"] = before });
        JsonElement deletion = events.StructuredContent!.Value.GetProperty("events")[0];
        Console.WriteLine($"event: {deletion.GetProperty("change").GetString()} {deletion.GetProperty("name").GetString()}");
        CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
        Console.WriteLine($"list_mailboxes: {boxes.StructuredContent!.Value.GetProperty("mailboxes")[0].GetProperty("message_count")} message(s) left, seen in the same store");
        Reply status = await Curl("-u", who, "-X", "STATUS INBOX (MESSAGES)", imap);
        Console.WriteLine($"IMAP: {status.Out.Trim()}");
        break;
    default:
        string text = (await Curl("-u", who, pop3 + "1")).Out;
        Console.WriteLine($"RETR 1: {text.Split("\r\n")[2]}");
        await Delete1();
        await Store();
        break;
}

return 0;

// The store as list_messages shows it: each message's UID and its Subject.
async Task Store()
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
    });
    JsonElement[] messages = listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray().ToArray();
    Console.WriteLine($"list_messages: {messages.Length} message(s), UIDs {string.Join(", ", messages.Select(m => m.GetProperty("uid").GetInt32()))}");
}

// DELE 1 is a POP3 command: the curl tool runs curl, which deletes when its session ends.
async Task Delete1()
{
    Reply deleted = await Curl("-X", "DELE 1", "-u", who, pop3);
    Console.WriteLine($"DELE 1: curl exit {deleted.Exit}");

    // The deletion is a store change like a delivery: wait for its event, with a bound, before reading the store.
    long next = await NextEvent(seen);
    if (next == 0)
    {
        Console.WriteLine("no deletion event within 10 s");
        return;
    }

    seen = next;
}

// POP3 answers a listing as "<number> <octets>" lines; the octets vary with the transport, so only the number is shown.
static string Shape(string line) => line.Split(' ')[0] + " <octets>";

// Waits at most ten seconds for the next mail store event after a sequence number; 0 means none came.
async Task<long> NextEvent(long after)
{
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["after_sequence"] = after, ["timeout_ms"] = 10000,
    });
    JsonElement answer = waited.StructuredContent!.Value;
    return answer.TryGetProperty("timed_out", out _) ? 0 : answer.GetProperty("event").GetProperty("sequence").GetInt64();
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<Reply> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return new Reply(answer.GetProperty("exitCode").GetInt32(), answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!);
}

record Reply(int Exit, string Out, string Err)
{
    public string[] Lines => Out.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}
