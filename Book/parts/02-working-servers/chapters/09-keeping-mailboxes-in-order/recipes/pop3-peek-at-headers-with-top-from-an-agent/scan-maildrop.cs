// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent scans a POP3 maildrop with UIDL and TOP through curl-mcp, chooses the message it wants
// by its Subject, and retrieves only that one in full.
// Run: dotnet run scan-maildrop.cs [-- <case>]
//   (no argument)  scan three messages, retrieve the delayed order
//   after-delete   delete message 1 first, then find the delayed order again by its unique ID
//   lines          TOP 2 2
//   whole          TOP 2 99 against the whole message
//   one-id         UIDL 2, read with -v
//   same-session   DELE 1 then UIDL in one session
//   missing        TOP for a message that is not there
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "scan";

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

// POP3 on a free port, example.com, one mailbox.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "pop3://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string pop3 = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Three orders, then a bounded wait for the delivery event of the third.
foreach (string subject in new[] { "Order 1 shipped", "Order 2 delayed", "Order 3 shipped" })
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "ann@example.com",
        ["message"] = $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nFirst line.\r\nSecond line.\r\n",
    });
}

CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail",
    ["name"] = "ann@example.com/INBOX/;UID=3", ["timeout_ms"] = 10000,
});
if (!waited.StructuredContent!.Value.TryGetProperty("event", out _))
{
    Console.WriteLine("the third message never arrived");
    return 1;
}

if (which == "missing")
{
    JsonElement failed = await Curl("-u", "ann@example.com:annpass", "-X", "TOP 9 0", pop3);
    Console.WriteLine($"TOP 9 0: curl exit {failed.GetProperty("exitCode")}: {Err(failed)}");
    return 1;
}

if (which == "lines")
{
    // TOP 2 2: the header, the blank line, and the first two lines of the body.
    string top = Out(await Curl("-u", "ann@example.com:annpass", "-X", "TOP 2 2", pop3));
    Console.Write($"TOP 2 2 body:\n{top.Split("\r\n\r\n")[1].Replace("\r\n", "\n")}");
    return 0;
}

if (which == "whole")
{
    // More lines than the body has: TOP answers the whole message, the same bytes as retrieving it.
    string top = Out(await Curl("-u", "ann@example.com:annpass", "-X", "TOP 2 99", pop3));
    string whole = Out(await Curl("-u", "ann@example.com:annpass", pop3 + "2"));
    Console.WriteLine($"TOP 2 99: {top.Length} characters; retrieved 2: {whole.Length}; same: {top == whole}");
    return 0;
}

if (which == "one-id")
{
    // UIDL with a number answers on its status line, which curl does not print; -v shows it.
    JsonElement one = await Curl("-v", "-u", "ann@example.com:annpass", "-X", "UIDL 2", pop3);
    string reply = one.GetProperty("standardError").GetProperty("content").GetString()!
        .Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("< +OK 2 "));
    Console.WriteLine($"UIDL 2: standard output {Out(one).Length} characters; -v {reply}");
    return 0;
}

if (which == "same-session")
{
    // --next keeps one session: after DELE 1, UIDL still lists 2 and 3 under their old numbers.
    string listed = Out(await Curl("-u", "ann@example.com:annpass", "-X", "DELE 1", pop3, "--next", "-u", "ann@example.com:annpass", "-X", "UIDL", pop3));
    Console.Write($"same session:\n{listed.Replace("\r\n", "\n")}");
    return 0;
}

if (which == "after-delete")
{
    // DELE takes effect when the session ends; the next session numbers the messages again.
    await Curl("-u", "ann@example.com:annpass", "-X", "DELE 1", pop3);
    Console.WriteLine("deleted message 1");
}

// The scan: UIDL for the numbers and unique IDs, then TOP n 0 for each header.
string wanted = "";
foreach (string line in Out(await Curl("-u", "ann@example.com:annpass", "-X", "UIDL", pop3)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
{
    string number = line.Split(' ')[0];
    string header = Out(await Curl("-u", "ann@example.com:annpass", "-X", $"TOP {number} 0", pop3));
    string subject = header.Split("\r\n").Single(h => h.StartsWith("Subject: "));
    Console.WriteLine($"{line}: {subject}");
    if (subject.EndsWith("delayed"))
    {
        wanted = number;
    }
}

// Only the chosen message is retrieved in full: a POP3 URL ending in its number is RETR.
string full = Out(await Curl("-u", "ann@example.com:annpass", pop3 + wanted));
Console.WriteLine($"retrieved {wanted}: {full.Split("\r\n\r\n")[1].Split("\r\n")[0]}");
return 0;

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
string Err(JsonElement answer) => answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim();

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
