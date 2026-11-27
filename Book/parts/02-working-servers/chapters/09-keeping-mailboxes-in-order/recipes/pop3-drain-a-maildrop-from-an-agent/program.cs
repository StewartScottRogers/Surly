// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent drains a POP3 maildrop: surl-mcp starts the mail server and seeds three messages, curl-mcp's curl tool
// retrieves each into a file and deletes each, and surl-mcp's list_messages confirms the store is empty. There is
// no delete tool: a POP3 client deletes. Over the official MCP SDK.
// Run: dotnet run program.cs -- <case>
//   drain         retrieve messages 1 to 3 into files, then DELE each (the default)
//   whole         the files hold the three orders, byte for byte what was seeded
//   next-session  a new session lists nothing
//   count         the store after the drain, with list_messages
//   empty         retrieve message 1 of the drained maildrop
//   listing       the listing before the drain: three lines, sizes list_messages gives
//   unwritable    the folder for the files does not exist: the save fails before any DELE
//   unwritable-kept  the same, and the store after it
//   rset          DELE each, then RSET, in one session
//   rset-kept     the same, and a new session's listing
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "drain";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The files go in a folder of their
// own under it, made for this case and removed at the end.
string here = Directory.GetCurrentDirectory();
string folder = "drain-" + which;
Directory.CreateDirectory(Path.Combine(here, folder));

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

// POP3 on a free port for example.com, one mailbox.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "pop3://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string pop3 = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string who = "ann@example.com:annpass";
long lastEnded = 0;

try
{
    // Three orders, put in as a delivery does; seed_mail answers once each is stored.
    string[] subjects = ["Order 1 shipped", "Order 2 shipped", "Order 3 shipped"];
    foreach (string subject in subjects)
    {
        await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["to"] = "ann@example.com", ["message"] = Order(subject),
        });
    }

    switch (which)
    {
        case "listing":
            Reply listing = await Curl("-u", who, pop3);
            JsonElement[] stored = (await Messages()).ToArray();
            bool same = listing.Lines.Select(l => long.Parse(l.Split(' ')[1])).SequenceEqual(stored.Select(m => m.GetProperty("size").GetInt64()));
            Console.WriteLine($"listing: {listing.Lines.Length} messages, octets match list_messages: {same}");
            return 0;
        case "unwritable":
        case "unwritable-kept":
            Reply failed = await Drain("nosuch");
            Console.WriteLine($"drain: tool error, no DELE sent: {failed.Err}");
            if (which == "unwritable-kept")
            {
                Console.WriteLine($"in the store: {(await Messages()).Count()}");
            }

            return 0;
        case "rset":
        case "rset-kept":
            // DELE marks each message; RSET, in the same curl run and so the same session, unmarks every one.
            Reply undone = await Curl("-X", "DELE", "-u", who, pop3 + "[1-3]", "--next", "-X", "RSET", "-u", who, pop3);
            Console.WriteLine($"DELE for each, then RSET in one session: curl exit {undone.Exit}");
            if (which == "rset-kept")
            {
                Console.WriteLine($"next session lists {(await Curl("-u", who, pop3)).Lines.Length} messages");
            }

            return 0;
    }

    // The drain: every message is retrieved into a file first, and only then deleted, in one curl run.
    Reply drained = await Drain(folder);

    switch (which)
    {
        case "whole":
            foreach (int number in Enumerable.Range(1, subjects.Length))
            {
                string text = File.ReadAllText(Path.Combine(here, folder, $"msg{number}.eml"));
                Console.WriteLine($"msg{number}.eml: {text.Split("\r\n").First(h => h.StartsWith("Subject: "))}, same as seeded: {text == Order(subjects[number - 1])}");
            }

            return 0;
        case "next-session":
            Console.WriteLine($"next session lists {(await Curl("-u", who, pop3)).Lines.Length} messages");
            return 0;
        case "count":
            Console.WriteLine($"in the store after the drain: {(await Messages()).Count()}");
            return 0;
        case "empty":
            Reply again = await Curl("-v", "-u", who, pop3 + "1");
            Console.WriteLine($"RETR 1 of the drained maildrop: curl exit {again.Exit}, {again.Err.Split('\n').First(line => line.Contains("-ERR")).Trim()}");
            return 0;
        default:
            Console.WriteLine($"drain: curl exit {drained.Exit}, files {Directory.GetFiles(Path.Combine(here, folder), "msg*.eml").Length}");
            return 0;
    }
}
finally
{
    // The server is stopped before its folder goes.
    await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = id });
    Directory.Delete(Path.Combine(here, folder), true);
}

// One order as a message: the lines end in carriage return and line feed, as a message's do.
static string Order(string subject) =>
    $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nYour order is on its way.\r\n";

// One curl run is the whole drain: retrieve [1-3] into files, and after --next delete [1-3], over one POP3 session.
// The URL's range is curl's, so the tool is given one URL for three message numbers, and -o names a file for each.
Task<Reply> Drain(string into) =>
    Curl("-u", who, "-o", $"{into}/msg#1.eml", pop3 + "[1-3]", "--next", "-u", who, "-X", "DELE", pop3 + "[1-3]");

// The store as list_messages shows it, read with no client: one element a message.
async Task<IEnumerable<JsonElement>> Messages()
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
    });
    return listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray().ToArray();
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out and -S
// puts an error message back. A deletion takes effect when the session ends with QUIT, which curl sends when its
// run ends, so the agent waits for the server to record the end of that connection, with a bound that only a failure
// reaches, before it reads anything the run changed. Each run is one connection, so each wait takes the next event.
async Task<Reply> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").Prepend("-S").ToArray() });
    string text = result.Content.OfType<TextContentBlock>().Single().Text;
    if (!text.StartsWith('{'))
    {
        // The tool could not run curl at all: a tool error with a plain text, and no session to wait for.
        return new Reply(-1, "", text);
    }

    JsonElement answer = JsonDocument.Parse(text).RootElement;
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["after_sequence"] = lastEnded, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
    });
    JsonElement found = waited.StructuredContent!.Value;
    JsonElement ended = found.TryGetProperty("event", out JsonElement inner) ? inner : found;
    if (!ended.TryGetProperty("sequence", out JsonElement sequence))
    {
        throw new TimeoutException("the server did not end curl's session in 10 s");
    }

    lastEnded = sequence.GetInt64();
    return new Reply(answer.GetProperty("exitCode").GetInt32(), answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!);
}

record Reply(int Exit, string Out, string Err)
{
    public string[] Lines => Out.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}
