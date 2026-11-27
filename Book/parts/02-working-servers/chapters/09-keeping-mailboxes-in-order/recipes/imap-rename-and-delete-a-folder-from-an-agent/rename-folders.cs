// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tidies a mailbox's folders over IMAP through curl-mcp, RENAME and DELETE, reads the
// server's refusal when a command is wrong, and checks each step against surl-mcp's list_mailboxes.
// Run: dotnet run rename-folders.cs [-- <case>]
//   (no argument)  RENAME Old Archive, then LIST
//   keeps          the renamed folder's messages
//   gone           STATUS of the old name
//   delete         DELETE Drafts
//   no-rename      RENAME a folder that does not exist
//   no-delete      DELETE a folder that is already gone
//   onto           RENAME onto a name in use
//   delete-inbox   DELETE INBOX
//   rename-inbox   RENAME INBOX
//   delete-full    DELETE a folder that holds messages
//   path           RENAME to a name with a slash
//   parent         DELETE the implied parent of that path
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "rename";

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

// IMAP on a free port, example.com, ann with the folders Old, Drafts and Keep.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass;folders=Old,Drafts,Keep" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
const string Ann = "ann@example.com:annpass";

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// Two orders into Old and one into INBOX with IMAP APPEND, which is curl -T; each is stored before curl exits.
// curl-mcp reads the two files from CURL_MCP_ROOT, which recipe.yml's fixtures wrote.
await Curl("-u", Ann, "-T", "first.eml", imap + "Old");
await Curl("-u", Ann, "-T", "second.eml", imap + "Old");
await Curl("-u", Ann, "-T", "first.eml", imap + "INBOX");

// One IMAP command with -X; a URL with no folder sends it straight after the login.
Task<JsonElement> Command(string text) => Curl("-u", Ann, "-X", text, imap);

// A refusal is curl's exit code and the server's tagged reply, read from -v.
async Task Refused(string text)
{
    JsonElement failed = await Curl("-v", "-u", Ann, "-X", text, imap);
    string reply = failed.GetProperty("standardError").GetProperty("content").GetString()!
        .Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("< A00") && (l.Contains(" NO ") || l.Contains(" BAD ")))
        ?? "(no refusal)";
    Console.WriteLine($"{text}: curl exit {failed.GetProperty("exitCode")}, {reply}");
}

// The server's own view: each folder and how many messages it holds, from list_mailboxes.
async Task<string> Folders()
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
    return string.Join(", ", boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray()
        .Select(m => $"{m.GetProperty("name").GetString()} {m.GetProperty("message_count")}"));
}

switch (mode)
{
    case "rename":
        await Command("RENAME Old Archive");
        Console.WriteLine(Out(await Curl("-u", Ann, imap)));
        break;

    case "keeps":
        await Command("RENAME Old Archive");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "gone":
        await Command("RENAME Old Archive");
        await Refused("STATUS Old (MESSAGES)");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "delete":
        await Command("DELETE Drafts");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "no-rename":
        await Refused("RENAME Nowhere Elsewhere");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "no-delete":
        await Command("DELETE Drafts");
        await Refused("DELETE Drafts");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "onto":
        await Refused("RENAME Keep Old");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "delete-inbox":
        await Refused("DELETE INBOX");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "rename-inbox":
        Console.WriteLine($"RENAME INBOX Mail2025: curl exit {(await Curl("-u", Ann, "-X", "RENAME INBOX Mail2025", imap)).GetProperty("exitCode")}");
        Console.WriteLine($"folders: {await Folders()}");
        Console.WriteLine($"STATUS INBOX: {Out(await Command("STATUS INBOX (MESSAGES)"))}");
        break;

    case "delete-full":
        Console.WriteLine($"before: {await Folders()}");
        Console.WriteLine($"DELETE Old: curl exit {(await Curl("-u", Ann, "-X", "DELETE Old", imap)).GetProperty("exitCode")}");
        Console.WriteLine($"after: {await Folders()}");
        break;

    case "path":
        await Command("RENAME Keep Work/Notes");
        Console.WriteLine(Out(await Curl("-u", Ann, imap)));
        break;

    case "parent":
        await Command("RENAME Keep Work/Notes");
        await Refused("DELETE Work");
        Console.WriteLine($"folders: {await Folders()}");
        break;
}

return 0;
