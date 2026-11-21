// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a mail server whose mailboxes already hold Sent, Drafts and Archive, lists them
// with surl-mcp's list_mailboxes and over IMAP with curl-mcp, uses them, and resets the server.
// Run: dotnet run start-folders.cs [-- <case>]
//   (no argument)  ann and bob with three folders, carol with none, dave and erin with odd names
//   twice          a folders= list that names Sent twice
//   empty          folders= with nothing after it
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "folders";

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

// IMAP on a free port. ann and bob start with three folders, carol with INBOX only,
// dave with a nested name and one with a space, erin with a comma written %2C.
string folders = which switch { "twice" => "Sent,Sent", "empty" => "", _ => "Sent,Drafts,Archive" };
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com",
        "--mailbox", $"ann@example.com:annpass;folders={folders}",
        "--mailbox", "bob@example.com:bobpass;folders=Sent,Drafts,Archive",
        "--mailbox", "carol@example.com:carolpass",
        "--mailbox", "dave@example.com:davepass;folders=Sent Items,Archive/2026",
        "--mailbox", "erin@example.com:erinpass;folders=Paid%2CUnpaid",
    },
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    Console.WriteLine($"start_server: exit_code {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")}), {server.GetProperty("lines")[0]}");
    return 1;
}

string id = server.GetProperty("server_id").GetString()!;
string imap = server.GetProperty("bound_urls")[0].GetString()!;

Console.WriteLine($"list_mailboxes ann: {await Mailboxes("ann@example.com")}");
Console.WriteLine($"curl ann: {Names(await Curl("-u", "ann@example.com:annpass", imap))}");
Console.WriteLine($"curl carol: {Names(await Curl("-u", "carol@example.com:carolpass", imap))}");
Console.WriteLine($"STATUS ann Sent: {(await Curl("-u", "ann@example.com:annpass", "-X", "STATUS Sent (MESSAGES)", imap)).Trim()}");

// Names: LIST quotes a space, nests on the slash and implies the parent; %2C is a comma.
Console.Write((await Curl("-u", "dave@example.com:davepass", imap)).Replace("\r\n", "\n"));
Console.WriteLine($"list_mailboxes erin: {await Mailboxes("erin@example.com")}");

// The folders are each mailbox's own: a message ann's client files in her Sent (IMAP APPEND) is not in bob's.
File.WriteAllText(Path.Combine(here, "sent.eml"), "From: ann@example.com\r\nTo: bob@example.com\r\nSubject: Minutes\r\n\r\nThe minutes are attached.\r\n");
await Curl("-u", "ann@example.com:annpass", "-T", "sent.eml", imap + "Sent");
Console.WriteLine($"after APPEND, ann: {await Mailboxes("ann@example.com")}");
Console.WriteLine($"after APPEND, bob: {await Mailboxes("bob@example.com")}");

// A client may delete a start folder.
await Curl("-u", "ann@example.com:annpass", "-X", "DELETE Drafts", imap);
Console.WriteLine($"after DELETE Drafts: {Names(await Curl("-u", "ann@example.com:annpass", imap))}");

// Between tests: reset_server empties every mailbox and puts back the start folders, deleted ones too.
await surl.CallToolAsync("seed_mail", new Dictionary<string, object?> { ["server_id"] = id, ["to"] = "ann@example.com", ["message"] = "Subject: Hello\r\n\r\nHello.\r\n" });
Console.WriteLine($"after seed_mail: {await Mailboxes("ann@example.com")}");
await surl.CallToolAsync("reset_server", new Dictionary<string, object?> { ["server_id"] = id });
Console.WriteLine($"after reset_server: {await Mailboxes("ann@example.com")}");
Console.WriteLine($"after reset_server, bob: {await Mailboxes("bob@example.com")}");
return 0;

// The store's view: list_mailboxes, each folder with its message count.
async Task<string> Mailboxes(string owner)
{
    CallToolResult listed = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
    return string.Join(", ", listed.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray()
        .Select(m => $"{m.GetProperty("name").GetString()} ({m.GetProperty("message_count")})"));
}

// The client's view: an IMAP URL with no folder lists them, one "* LIST" line each, name last.
static string Names(string list) =>
    string.Join(", ", list.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split(" \"/\" ")[1]));

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<string> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement
        .GetProperty("standardOutput").GetProperty("content").GetString()!;
}
