// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds a conversation, a question and two replies linked by Message-ID, In-Reply-To and
// References, with an unrelated message between them, then reads the envelopes over IMAP as a mail
// client would and groups them into a thread.
// Run: dotnet run seed-thread.cs -- <case>
//   group       fetch every ENVELOPE and print the threads (the default)
//   envelope    the first reply's ENVELOPE, as IMAP sends it
//   headers     read_message: the last reply's linking headers, as seeded
//   references  UID SEARCH HEADER References for the first message's ID
//   no-thread   the server's CAPABILITY, and the THREAD command
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "group";

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

// An IMAP server for example.com with Ann's mailbox.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The conversation, in the order it reached Ann. Each reply names the message it answers in
// In-Reply-To and the whole chain in References.
string[] messages =
[
    Message("bob", "Lunch on Friday?", "<lunch-1@example.com>", null, "Shall we try the new place?"),
    Message("billing", "Invoice 1002", "<inv-1002@example.com>", null, "Your invoice is attached."),
    Message("cy", "Re: Lunch on Friday?", "<lunch-2@example.com>", "<lunch-1@example.com>", "Count me in."),
    Message("bob", "Re: Lunch on Friday?", "<lunch-3@example.com>", "<lunch-1@example.com> <lunch-2@example.com>", "Noon, then."),
];
foreach (string message in messages)
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?> { ["server_id"] = id, ["to"] = "ann@example.com", ["message"] = message });
}

switch (which)
{
    case "headers":
        CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 4,
        });
        foreach (string line in read.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n")
            .Where(l => l.StartsWith("Message-ID:") || l.StartsWith("In-Reply-To:") || l.StartsWith("References:")))
        {
            Console.WriteLine(line);
        }

        return 0;
    case "references":
        Console.Write(await Imap("UID SEARCH HEADER References \"<lunch-1@example.com>\"", "INBOX"));
        return 0;
    case "envelope":
        Console.Write(await Imap("UID FETCH 3 ENVELOPE", "INBOX"));
        return 0;
    case "no-thread":
        Console.Write(await Imap("CAPABILITY", ""));
        Console.Write(await Imap("THREAD REFERENCES UTF-8 ALL", "INBOX"));
        return 0;
}

// What a mail client reads to thread: each message's ENVELOPE, whose last two fields are
// In-Reply-To and Message-ID.
var envelope = new Regex("""^\* \d+ FETCH \(UID (\d+) ENVELOPE \(NIL "([^"]*)" .* (NIL|"<[^>]*>") "(<[^>]*>)"\)\)$""");
var found = new List<(string Uid, string Subject, string? InReplyTo, string MessageId)>();
foreach (string line in (await Imap("UID FETCH 1:* ENVELOPE", "INBOX")).Split('\n', StringSplitOptions.RemoveEmptyEntries))
{
    Match m = envelope.Match(line);
    found.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value == "NIL" ? null : m.Groups[3].Value.Trim('"'), m.Groups[4].Value));
}

// The threads: a message that answers none starts one; each reply goes under the message it answers.
foreach (var root in found.Where(f => f.InReplyTo is null))
{
    Print(root, 0);
}

return 0;

void Print((string Uid, string Subject, string? InReplyTo, string MessageId) message, int depth)
{
    Console.WriteLine($"{new string(' ', depth * 2)}UID {message.Uid}: {message.Subject}");
    foreach (var reply in found.Where(f => f.InReplyTo == message.MessageId))
    {
        Print(reply, depth + 1);
    }
}

// In-Reply-To names the message answered: the last ID in References.
static string Message(string from, string subject, string id, string? references, string body) =>
    $"From: {from}@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\nMessage-ID: {id}\r\n"
    + (references is null ? "" : $"In-Reply-To: {references.Split(' ')[^1]}\r\nReferences: {references}\r\n")
    + $"\r\n{body}\r\n";

// The curl tool sends one IMAP command, logged in as Ann, on a folder; -s and -S as usual.
async Task<string> Imap(string command, string folder)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", "ann@example.com:annpass", "-X", command, imap + folder },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
    return exit == 0
        ? answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n")
        : $"{command}: curl exit {exit}, {answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}\n";
}
