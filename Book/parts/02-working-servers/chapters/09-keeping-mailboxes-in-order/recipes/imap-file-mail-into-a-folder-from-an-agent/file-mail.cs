// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent files mail over IMAP through curl-mcp, CREATE, COPY or MOVE, STORE and EXPUNGE, and checks
// each step against surl-mcp's list_mailboxes and list_messages.
// Run: dotnet run file-mail.cs [-- <case>]
//   (no argument)  create, copy, mark deleted, expunge
//   exists         CREATE a folder that exists
//   no-folder      COPY into a folder that does not exist
//   no-message     COPY a message number the folder does not have
//   keeps          COPY alone
//   flags          COPY a flagged message
//   deleted        mark a message deleted, no EXPUNGE
//   expunge        EXPUNGE it
//   move           MOVE
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "file";

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
    if (!waited.StructuredContent!.Value.TryGetProperty("event", out _))
    {
        Console.WriteLine($"message {n} never arrived");
        return 1;
    }
}

// The server's own view: how many messages each folder holds.
async Task<string> Counts()
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
    return string.Join(", ", boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray()
        .Select(m => $"{m.GetProperty("name").GetString()} {m.GetProperty("message_count")}"));
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// One IMAP command with -X; the folder in the URL is selected first. -v adds the server's replies to stderr.
Task<JsonElement> Command(string folder, string text) => Curl("-u", Ann, "-X", text, imap + folder);

// A refusal is curl's exit code and the server's tagged reply, read from -v.
async Task Refused(string folder, string text)
{
    JsonElement failed = await Curl("-v", "-u", Ann, "-X", text, imap + folder);
    string reply = failed.GetProperty("standardError").GetProperty("content").GetString()!
        .Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("< A00") && (l.Contains(" NO ") || l.Contains(" BAD ")));
    Console.WriteLine($"{text}: curl exit {failed.GetProperty("exitCode")}, {reply}");
}

switch (mode)
{
    case "exists":
        await Command("", "CREATE Archive");
        await Refused("", "CREATE Archive");
        Console.WriteLine($"folders: {await Counts()}");
        return 0;

    case "no-folder":
        await Refused("INBOX", "COPY 1 Nowhere");
        Console.WriteLine($"folders: {await Counts()}");
        return 0;

    case "no-message":
        await Command("", "CREATE Archive");
        await Refused("INBOX", "COPY 9 Archive");
        Console.WriteLine($"folders: {await Counts()}");
        return 0;
}

await Command("", "CREATE Archive");

switch (mode)
{
    case "keeps":
        await Command("INBOX", "COPY 2 Archive");
        Console.WriteLine($"after COPY 2: {await Counts()}");
        return 0;

    case "flags":
        await Command("INBOX", @"STORE 2 +FLAGS (\Flagged)");
        await Command("INBOX", "COPY 2 Archive");
        Console.WriteLine($"FLAGGED in Archive: {Out(await Curl("-u", Ann, imap + "Archive?FLAGGED"))}");
        Console.WriteLine($"FLAGGED in INBOX: {Out(await Curl("-u", Ann, imap + "INBOX?FLAGGED"))}");
        return 0;

    case "move":
        Console.WriteLine($"before: {await Counts()}");
        JsonElement moved = await Curl("-v", "-u", Ann, "-X", "MOVE 1 Archive", imap + "INBOX");
        Console.WriteLine($"MOVE 1 Archive: {moved.GetProperty("standardError").GetProperty("content").GetString()!.Split('\n').Select(l => l.Trim()).First(l => l.EndsWith("OK MOVE completed"))}");
        Console.WriteLine($"after MOVE 1: {await Counts()}");
        return 0;
}

// The filing itself: copy order 1, mark the original deleted, expunge it.
await Command("INBOX", "COPY 1 Archive");
Console.WriteLine($"copied: {await Counts()}");
Console.WriteLine($"STORE: {Out(await Command("INBOX", @"STORE 1 +FLAGS (\Deleted)"))}");

if (mode == "deleted")
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
    });
    JsonElement first = listed.StructuredContent!.Value.GetProperty("messages")[0];
    Console.WriteLine($"DELETED in INBOX: {Out(await Curl("-u", Ann, imap + "INBOX?DELETED"))}");
    Console.WriteLine($"list_messages UID {first.GetProperty("uid")}: {string.Join(" ", first.GetProperty("flags").EnumerateArray().Select(f => f.GetString()))}");
    Console.WriteLine($"marked: {await Counts()}");
    return 0;
}

Console.WriteLine($"EXPUNGE: {Out(await Command("INBOX", "EXPUNGE"))}");
Console.WriteLine($"filed: {await Counts()}");
if (mode == "expunge")
{
    Console.WriteLine($"STATUS: {Out(await Command("", "STATUS INBOX (MESSAGES UIDNEXT)"))}");
}

return 0;
