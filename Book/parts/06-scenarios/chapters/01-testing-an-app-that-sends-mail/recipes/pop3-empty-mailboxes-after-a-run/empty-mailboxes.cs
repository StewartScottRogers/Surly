// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// After a run, an agent lists the mail left on a surl-mcp server, deletes it over POP3 with
// curl-mcp's curl tool, and checks every mailbox is empty, in the store and over IMAP.
// Run: dotnet run empty-mailboxes.cs -- <case>
//   empty           delete everything over POP3 (the default)
//   wrong-password  delete ann's mail with the wrong password
//   reset           reset_server instead of POP3
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "empty";

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

// POP3 and IMAP on free ports over one mail store: example.com, two mailboxes.
var passwords = new Dictionary<string, string> { ["ann@example.com"] = "annpass", ["bob@example.com"] = "bobpass" };
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "pop3://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string pop3 = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

// The run: it left two messages for ann and one for bob.
foreach (string to in new[] { "ann@example.com", "ann@example.com", "bob@example.com" })
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = to, ["message"] = "Subject: Welcome\r\n\r\nThanks for signing up.\r\n",
    });
}

// What is left, mailbox by mailbox.
Dictionary<string, int> left = await Counts("left");

if (which == "reset")
{
    // The whole server back to its state at start, which had no mail.
    CallToolResult reset = await surl.CallToolAsync("reset_server", new Dictionary<string, object?> { ["server_id"] = id });
    Console.WriteLine($"reset_server: tool error {(reset.IsError == true ? "yes" : "no")}");
}
else
{
    // Over POP3, one session per mailbox: DELE each message number, then QUIT commits them.
    foreach ((string owner, int count) in left.Where(pair => pair.Value > 0))
    {
        string password = which == "wrong-password" && owner == "ann@example.com" ? "wrong" : passwords[owner];
        List<string> arguments = ["-s", "-S"];
        for (int n = 1; n <= count; n++)
        {
            arguments.AddRange([.. (n > 1 ? new[] { "--next" } : []), "-u", $"{owner}:{password}", "-X", $"DELE {n}", pop3]);
        }

        CallToolResult deleted = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
        using JsonDocument answer = JsonDocument.Parse(deleted.Content.OfType<TextContentBlock>().Single().Text);
        string error = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
        Console.WriteLine($"DELE 1 to {count} for {owner}: curl exit {answer.RootElement.GetProperty("exitCode")}{(error == "" ? "" : $", {error}")}");
    }
}

// The check: every mailbox empty in the store, and ann's INBOX empty to an IMAP client too.
Dictionary<string, int> after = await Counts("after");
CallToolResult search = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-u", "ann@example.com:annpass", imap + "INBOX?ALL" },
});
using (JsonDocument found = JsonDocument.Parse(search.Content.OfType<TextContentBlock>().Single().Text))
{
    Console.WriteLine($"IMAP SEARCH ALL for ann: {found.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd()}");
}

return after.Values.All(count => count == 0) ? 0 : 1;

// list_mailboxes for each owner: the message count of its INBOX.
async Task<Dictionary<string, int>> Counts(string when)
{
    var counts = new Dictionary<string, int>();
    foreach (string owner in passwords.Keys)
    {
        CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
        counts[owner] = boxes.StructuredContent!.Value.GetProperty("mailboxes")[0].GetProperty("message_count").GetInt32();
        Console.WriteLine($"{when}: {owner} message_count {counts[owner]}");
    }

    return counts;
}
