// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds mail on a running surl server through surl-mcp, reads the store with its tools,
// and reads and sends mail with curl through curl-mcp. Run: dotnet run seed.cs -- <case>
//   solution            seed a message for alice, read it over IMAP as UID 1
//   imap-marks-it-seen  list_messages after the IMAP fetch
//   read-without-client read_message on a second seeded message, then list_mailboxes
//   smtp-needs-a-login  curl sends over SMTP without a login, then with one
//   no-such-account     seed_mail to carol, who has no account
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// One server, two listeners on free ports sharing one mail store, and one account: alice.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "alice:secret" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

const string Hello = "From: bob@example.com\r\nTo: alice@example.com\r\nSubject: Hello\r\n\r\nHello from surl.\r\n";
const string Again = "From: bob@example.com\r\nTo: alice@example.com\r\nSubject: Again\r\n\r\nStill here.\r\n";

switch (which)
{
    case "solution":
        await Seed("alice@example.com", Hello);
        await Curl("-u", "alice:secret", imap + "INBOX;UID=1");
        break;
    case "imap-marks-it-seen":
        await Seed("alice@example.com", Hello);
        await Curl("-u", "alice:secret", imap + "INBOX;UID=1");
        await ListMessages(0);
        break;
    case "read-without-client":
        await Seed("alice@example.com", Hello);
        await Curl("-u", "alice:secret", imap + "INBOX;UID=1");
        await Seed("alice@example.com", Again);
        CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["owner"] = "alice", ["mailbox"] = "INBOX", ["uid"] = 2,
        });
        JsonElement message = read.StructuredContent!.Value;
        Console.WriteLine($"read_message uid {message.GetProperty("uid")}: {message.GetProperty("text").GetRawText()}");
        CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["owner"] = "alice",
        });
        foreach (JsonElement box in boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray())
        {
            Console.WriteLine($"list_mailboxes: {box.GetProperty("name")}, {box.GetProperty("message_count")} messages, "
                + $"{box.GetProperty("unseen_count")} unseen");
        }

        break;
    case "smtp-needs-a-login":
        // --mail-from and --mail-rcpt are the envelope; -T sends note.eml, a file in this folder.
        await Seed("alice@example.com", Hello);
        await Curl("-S", "--mail-from", "alice@example.com", "--mail-rcpt", "alice@example.com", "-T", "note.eml", smtp);
        await Curl("-S", "-u", "alice:secret", "--mail-from", "alice@example.com", "--mail-rcpt", "alice@example.com", "-T", "note.eml", smtp);
        await ListMessages(1);
        break;
    case "no-such-account":
        await Seed("carol@example.com", "Subject: Lost\r\n\r\nNobody.\r\n");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// seed_mail delivers into the store as SMTP would, with no client and no login.
async Task Seed(string to, string message)
{
    CallToolResult result = await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["to"] = to, ["message"] = message,
    });
    Console.WriteLine(result.IsError == true
        ? $"seed_mail {to}: error: {result.Content.OfType<TextContentBlock>().Single().Text}"
        : $"seed_mail {to}: {result.StructuredContent!.Value.GetRawText()}");
}

// list_messages answers each message's UID, size and flags, after the UID given.
async Task ListMessages(int afterUid)
{
    CallToolResult result = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["owner"] = "alice", ["mailbox"] = "INBOX", ["after_uid"] = afterUid,
    });
    foreach (JsonElement m in result.StructuredContent!.Value.GetProperty("messages").EnumerateArray())
    {
        Console.WriteLine($"list_messages: uid {m.GetProperty("uid")}, size {m.GetProperty("size")}, flags {m.GetProperty("flags").GetRawText()}");
    }
}

// The curl tool runs curl's own arguments; -s keeps the progress meter out of the answer.
async Task Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = arguments.Prepend("-s").ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    string error = root.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    Console.WriteLine($"curl exit {root.GetProperty("exitCode")}, output "
        + root.GetProperty("standardOutput").GetProperty("content").GetRawText() + (error == "" ? "" : $", error {error}"));
}
