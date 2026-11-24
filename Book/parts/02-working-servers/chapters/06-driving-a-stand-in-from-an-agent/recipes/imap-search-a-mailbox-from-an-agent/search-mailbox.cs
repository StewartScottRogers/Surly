// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds a mailbox, searches it by subject over IMAP with curl-mcp's curl tool, and fetches
// each match by its UID.
// Run: dotnet run search-mailbox.cs -- <case>
//   search    UID SEARCH SUBJECT invoice, then fetch each match (the default)
//   phrase    UID SEARCH SUBJECT "invoice 1001"
//   none      UID SEARCH SUBJECT refund
//   numbers   after UID 2 is deleted, the URL query's SEARCH beside UID SEARCH
//   seen      search, fetch, then list_messages' flags
//   missing   fetch UID 9, which does not exist
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "search";

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

// An IMAP server for example.com with Ann's mailbox, seeded with five messages, UIDs 1 to 5.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string inbox = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "INBOX";
foreach (string subject in new[] { "Invoice 1001 for October", "Your order has shipped", "Invoice 1002 for November", "Team lunch on Friday", "Overdue: invoice 1001" })
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "ann@example.com",
        ["message"] = $"From: billing@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nSee the attached statement.\r\n",
    });
}

switch (which)
{
    case "missing":
        await Fetch(9);
        return 0;
    case "numbers":
        // Ann's client deletes UID 2; the messages after it are renumbered, their UIDs are not.
        await Curl(["-X", "UID STORE 2 +FLAGS (\\Deleted)", inbox]);
        await Curl(["-X", "EXPUNGE", inbox]);
        Console.Write($"URL query SUBJECT invoice: {Out(await Curl([inbox + "?SUBJECT%20invoice"]))}");
        Console.Write($"UID SEARCH SUBJECT invoice: {Out(await Curl(["-X", "UID SEARCH SUBJECT invoice", inbox]))}");
        return 0;
}

// The search: UID SEARCH answers UIDs, which stay the same for the life of the mailbox.
string criteria = which switch { "phrase" => "SUBJECT \"invoice 1001\"", "none" => "SUBJECT refund", _ => "SUBJECT invoice" };
string answer = Out(await Curl(["-X", $"UID SEARCH {criteria}", inbox]));
Console.Write($"UID SEARCH {criteria}: {answer}");
long[] uids = answer.Trim().Split(' ').Skip(2).Select(long.Parse).ToArray();

// Fetch each match by UID: only its Subject header field.
foreach (long uid in uids)
{
    await Fetch(uid);
}

if (which == "seen")
{
    // curl's fetch marks a message \Seen; list_messages shows which.
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
    });
    foreach (JsonElement message in listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray())
    {
        Console.WriteLine($"UID {message.GetProperty("uid")}: [{string.Join(" ", message.GetProperty("flags").EnumerateArray().Select(f => f.GetString()))}]");
    }
}

return 0;

// Fetch one message by UID: only its Subject header field, which curl prints with a blank line after.
async Task Fetch(long uid)
{
    JsonElement result = await Curl([$"{inbox};UID={uid};SECTION=HEADER.FIELDS%20(SUBJECT)"]);
    int exit = result.GetProperty("exitCode").GetInt32();
    Console.WriteLine(exit == 0
        ? $"UID {uid}: {Out(result).Trim()}"
        : $"UID {uid}: curl exit {exit}, {result.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
}

static string Out(JsonElement result) => result.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n");

// The curl tool, logged in as Ann; -s keeps the progress meter out, -S keeps an error line in.
async Task<JsonElement> Curl(string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", "ann@example.com:annpass" }.Concat(arguments).ToArray(),
    });
    using JsonDocument parsed = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return parsed.RootElement.Clone();
}
