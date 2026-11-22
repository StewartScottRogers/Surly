// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reports each mailbox of an account with its message and unread counts: surl-mcp starts a mail
// server with folders and seeds mail, curl-mcp files two sent mails over IMAP and reads one, and
// list_mailboxes answers the counts.
// Run: dotnet run report.cs -- <case>
//   report       every mailbox of ann with its counts
//   messages     list_messages for the INBOX and Sent after curl reads message 1: each message's flags
//   validity     the uid_validity of each mailbox, and the same on a second call
//   curl-read    curl reads message 1 of the INBOX: one fewer unread
//   read-message read_message reads message 2: no change
//   owners       bob's report, and ann's again
//   unknown      owners that have no mailbox
//   reset        reset_server: the mail goes, the folders stay
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "report";

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

// IMAP on a free port: ann has three extra folders, bob only an INBOX.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com",
        "--mailbox", "ann@example.com:annpass;folders=Sent,Drafts,Archive",
        "--mailbox", "bob@example.com:bobpass",
    },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Three orders arrive in ann's INBOX; each seed is waited for by its delivery event, with a bound.
long seen = 0;
for (int order = 1; order <= 3; order++)
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id,
        ["to"] = "ann@example.com",
        ["message"] = $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {order} shipped\r\n\r\nOrder {order} is on its way.\r\n",
    });
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["after_sequence"] = seen, ["timeout_ms"] = 10000,
    });
    JsonElement answer = waited.StructuredContent!.Value;
    if (answer.TryGetProperty("timed_out", out _))
    {
        Console.WriteLine($"order {order} did not arrive within 10 s");
        return 1;
    }

    seen = answer.GetProperty("event").GetProperty("sequence").GetInt64();
}

// Ann's mail client files two sent mails over IMAP APPEND; the curl tool is that client.
foreach (string sent in new[] { "sent1.eml", "sent2.eml" })
{
    JsonElement filed = await Curl("-u", "ann@example.com:annpass", "-T", sent, imap + "Sent");
    if (filed.GetProperty("exitCode").GetInt32() != 0)
    {
        Console.WriteLine($"{sent} was not filed");
        return 1;
    }
}

switch (mode)
{
    case "messages":
        await Curl("-u", "ann@example.com:annpass", imap + "INBOX;UID=1");
        foreach (string mailbox in new[] { "INBOX", "Sent" })
        {
            CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
            {
                ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = mailbox,
            });
            foreach (JsonElement message in listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray())
            {
                string flags = string.Join(" ", message.GetProperty("flags").EnumerateArray().Select(flag => flag.GetString()));
                Console.WriteLine($"list_messages {mailbox} UID {message.GetProperty("uid")}: flags [{flags}]");
            }
        }

        break;
    case "validity":
        JsonElement first = await List("ann@example.com");
        JsonElement second = await List("ann@example.com");
        bool positive = first.GetProperty("mailboxes").EnumerateArray().All(box => box.GetProperty("uid_validity").GetInt64() > 0);
        bool same = first.GetProperty("mailboxes").EnumerateArray().Zip(second.GetProperty("mailboxes").EnumerateArray())
            .All(pair => pair.First.GetProperty("uid_validity").GetInt64() == pair.Second.GetProperty("uid_validity").GetInt64());
        Console.WriteLine($"uid_validity positive on every mailbox: {positive}");
        Console.WriteLine($"uid_validity the same on a second call: {same}");
        return 0;
    case "curl-read":
        await Curl("-u", "ann@example.com:annpass", imap + "INBOX;UID=1");
        break;
    case "read-message":
        CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 2,
        });
        Console.WriteLine($"read_message UID 2: {read.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n")[2]}");
        break;
    case "owners":
        await Report("bob@example.com");
        break;
    case "unknown":
        foreach (string owner in new[] { "ann", "nobody@example.com" })
        {
            CallToolResult none = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
            Console.WriteLine($"{owner}: {(none.IsError == true ? "tool error" : "listed")}, {none.Content.OfType<TextContentBlock>().Single().Text}");
        }

        return 0;
    case "reset":
        await surl.CallToolAsync("reset_server", new Dictionary<string, object?> { ["server_id"] = id });
        break;
}

await Report("ann@example.com");
return 0;

// list_mailboxes answers the structured content {"mailboxes": [{name, message_count, unseen_count, uid_validity}]}.
async Task<JsonElement> List(string owner)
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
    return boxes.StructuredContent!.Value.Clone();
}

async Task Report(string owner)
{
    foreach (JsonElement box in (await List(owner)).GetProperty("mailboxes").EnumerateArray())
    {
        Console.WriteLine($"{owner} {box.GetProperty("name").GetString()}: {box.GetProperty("message_count")} messages, {box.GetProperty("unseen_count")} unseen");
    }
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
