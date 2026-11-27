// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a shared team folder: surl-mcp runs a mail server that shares Shared/team between
// ann and bob, curl-mcp's curl tool appends, reads, flags and searches as the members, and
// surl-mcp's tools show what the store holds.
// Run: dotnet run team-folder.cs -- <case>
//   test        append as ann, flag as bob, search as ann, then read the store (the default)
//   non-member  carol, who is not a member, tries to read the folder
//   seed        the agent seeds mail for team@example.com
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "test";

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

// IMAP on a free port for example.com: three mailboxes, and a folder team shared by ann and bob.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass",
        "--mailbox", "carol@example.com:carolpass", "--shared-folder", "team=ann@example.com,bob@example.com",
    },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string team = server.GetProperty("bound_urls")[0].GetString()! + "Shared/team";

if (which == "seed")
{
    // The folder has no address, so seed_mail cannot reach it.
    CallToolResult seeded = await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "team@example.com", ["message"] = File.ReadAllText("notice.eml"),
    });
    Console.WriteLine($"seed_mail: error {seeded.IsError == true}, {seeded.Content.OfType<TextContentBlock>().Single().Text}");
    return 1;
}

// Ann appends notice.eml from this folder: -T on an IMAP URL is an APPEND.
Console.WriteLine($"ann appends: curl exit {(await Curl("-u", "ann@example.com:annpass", "-T", "notice.eml", team)).GetProperty("exitCode")}");

if (which == "non-member")
{
    JsonElement read = await Curl("-u", "carol@example.com:carolpass", team + ";UID=1");
    Console.WriteLine($"carol reads: curl exit {read.GetProperty("exitCode")}, {read.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
    foreach (string owner in new[] { "ann@example.com", "carol@example.com" })
    {
        CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
        var names = boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Select(box => box.GetProperty("name").GetString());
        Console.WriteLine($"list_mailboxes {owner}: {string.Join(", ", names)}");
    }

    return 1;
}

// Bob reads it and flags it; Ann searches for flagged mail in the same folder.
Console.WriteLine($"bob reads: {Out(await Curl("-u", "bob@example.com:bobpass", team + ";UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)"))}");
Console.WriteLine($"bob flags: {Out(await Curl("-u", "bob@example.com:bobpass", "-X", @"STORE 1 +FLAGS (\Flagged)", team))}");
string found = Out(await Curl("-u", "ann@example.com:annpass", team + "?FLAGGED"));
Console.WriteLine($"ann searches: {found}");

// The store, read without a client: Ann's view of the folder lists the message and its flags.
CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "Shared/team",
});
JsonElement first = listed.StructuredContent!.Value.GetProperty("messages")[0];
Console.WriteLine($"list_messages: uid {first.GetProperty("uid")}, flags {string.Join(" ", first.GetProperty("flags").EnumerateArray().Select(flag => flag.GetString()))}");
return found == "* SEARCH 1" ? 0 : 1;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-sS").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// What curl printed, without the line ends.
static string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();
