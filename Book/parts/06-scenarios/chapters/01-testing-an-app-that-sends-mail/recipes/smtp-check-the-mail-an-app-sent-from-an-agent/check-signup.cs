// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests an app's sign-up mail: surl-mcp runs the mail server, curl-mcp's curl tool sends
// the mail as the app would and reads it back over IMAP, and surl-mcp's tools show what was stored.
// Run: dotnet run check-signup.cs -- <case>
//   right            the app sends signup.eml to ann; the test passes (the default)
//   wrong-subject    the app sends bad-subject.eml, whose subject is misspelt
//   wrong-recipient  the app mails an@example.com instead of ann@example.com
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "right";

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

// SMTP and IMAP on free ports over one mail store, serving example.com, with two mailboxes.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;
string imap = server.GetProperty("bound_urls")[1].GetString()!;

// The app's send: its login, the envelope, and the message from a file in this folder.
string file = which == "wrong-subject" ? "bad-subject.eml" : "signup.eml";
string to = which == "wrong-recipient" ? "an@example.com" : "ann@example.com";
JsonElement sent = await Curl("--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com",
    "--mail-rcpt", to, "-T", file, smtp + "example.com");
Console.WriteLine($"app sent to {to}: curl exit {sent.GetProperty("exitCode")}");

// The test: From, To and Subject of Ann's first message, compared with what the test expects.
JsonElement fetched = await Curl("-u", "ann@example.com:annpass", imap + "INBOX;UID=1;SECTION=HEADER.FIELDS%20(FROM%20TO%20SUBJECT)");
JsonElement output = fetched.GetProperty("standardOutput").GetProperty("content");
string fields = output.GetString()!;
const string Want = "From: app@example.com\r\nTo: ann@example.com\r\nSubject: Welcome to Example\r\n\r\n";
Console.WriteLine($"fetch: curl exit {fetched.GetProperty("exitCode")}, {output.GetRawText()}");
Console.WriteLine(fields == Want ? "PASS" : "FAIL");

if (which == "wrong-recipient")
{
    // What the server stored for Ann, and what it answered the app, read without a client.
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
    JsonElement inbox = boxes.StructuredContent!.Value.GetProperty("mailboxes")[0];
    Console.WriteLine($"list_mailboxes: {inbox.GetProperty("name")}, {inbox.GetProperty("message_count")} messages");
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
    string[] lines = exchanges.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
        .Select(c => $"{(c.GetProperty("direction").GetString() == "sent" ? "<" : ">")} {c.GetProperty("data").GetString()!.TrimEnd()}").ToArray();
    int rcpt = Array.FindIndex(lines, l => l.StartsWith("> RCPT TO:"));
    Console.WriteLine($"get_exchanges: {lines[rcpt]} / {lines[rcpt + 1]}");
}

return fields == Want ? 0 : 1;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
