// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a mail server for one domain with two mailboxes, sends between them with
// curl-mcp's curl tool, reads the mail over IMAP, and shows mail to another domain refused.
// Run: dotnet run serve-domain.cs -- <case>
//   deliver      Ann mails Bob; Bob reads it over IMAP (the default)
//   relay        Ann mails carol@elsewhere.org, outside the domain
//   unknown      Ann mails cy@example.com, in the domain with no mailbox
//   no-login     a send with no login
//   outside-box  start_server with a mailbox outside the domain
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "deliver";

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

// The mail server: SMTP to send, IMAP to read, for example.com, with Ann's and Bob's mailboxes.
string[] mailboxes = which == "outside-box"
    ? ["ann@example.com:annpass", "bob@example.org:bobpass"]
    : ["ann@example.com:annpass", "bob@example.com:bobpass"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", mailboxes[0], "--mailbox", mailboxes[1] },
});
if (started.IsError == true)
{
    JsonElement refusal = started.StructuredContent!.Value;
    Console.WriteLine($"start_server refused: exit_code {refusal.GetProperty("exit_code").GetInt32()}, {refusal.GetProperty("lines")[0].GetString()}");
    return 0;
}

string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

// The send: Ann logs in with her whole address, unless the case sends with no login.
string to = which switch { "relay" => "carol@elsewhere.org", "unknown" => "cy@example.com", _ => "bob@example.com" };
string[] login = which == "no-login" ? [] : ["-u", "ann@example.com:annpass"];
JsonElement sent = await Curl([.. login, "--mail-from", "ann@example.com", "--mail-rcpt", to, "-T", "memo.eml", "-w", "%{response_code}", smtp + "example.com"]);
int exit = sent.GetProperty("exitCode").GetInt32();
Console.WriteLine(exit == 0
    ? $"curl exit 0, reply {sent.GetProperty("standardOutput").GetProperty("content").GetString()}"
    : $"curl exit {exit}: {sent.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");

switch (which)
{
    case "relay":
        // The server's record of the exchange: the line it sent after RCPT TO.
        await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp" });
        CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
        List<string> lines = got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last().GetProperty("chunks").EnumerateArray()
            .SelectMany(c => c.GetProperty("data").GetString()!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)).ToList();
        int rcpt = lines.FindIndex(l => l.StartsWith("RCPT TO:"));
        Console.WriteLine($"{lines[rcpt]} answered {lines[rcpt + 1]}");
        break;
    case "unknown":
        CallToolResult listed = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = to });
        Console.WriteLine($"list_mailboxes {to}: {listed.Content.OfType<TextContentBlock>().Single().Text}");
        break;
    case "deliver":
        // Wait for the delivery, then Bob reads the Subject over IMAP with his own login.
        CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = to,
        });
        Console.WriteLine($"event: {waited.StructuredContent!.Value.GetProperty("event").GetProperty("name").GetString()}");
        JsonElement read = await Curl(["-u", "bob@example.com:bobpass", imap + "INBOX;UID=1;SECTION=HEADER.FIELDS%20(FROM%20SUBJECT)"]);
        Console.Write(read.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n"));
        break;
}

return 0;

// The curl tool: curl's own arguments; -s keeps the progress meter out, -S keeps an error line in.
async Task<JsonElement> Curl(string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", "-S" }.Concat(arguments).ToArray() });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.Clone();
}
