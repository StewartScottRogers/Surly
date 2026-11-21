// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reviews every mail an app sent: surl-mcp keeps all of the domain's mail in one catch-all
// mailbox, curl-mcp's curl tool sends as the app would, and the agent reads each message back with
// list_messages and read_message and checks it has From, Date and Message-ID and no outside address.
// Run: dotnet run review-mail.cs -- <case>
//   clean     the app sends two mails that keep the rules (the default)
//   broken    the app sends one good mail and one with no Date or Message-ID and a Cc outside the domain
//   outside   the app names a recipient outside the domain in the envelope
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "clean";
const string Domain = "example.com";

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

// SMTP for example.com; every address with no mailbox of its own lands in review@example.com.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", Domain, "--mailbox", "app@example.com:apppass", "--mailbox", "review@example.com:reviewpass",
        "--mail-catch-all", "example.com=review@example.com",
    },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The app's run: each mail, its envelope recipient and its file in this folder.
(string To, string File)[] sends = which switch
{
    "broken" => [("dan@example.com", "shipped.eml"), ("gus@example.com", "careless.eml")],
    "outside" => [("eve@elsewhere.org", "shipped.eml")],
    _ => [("dan@example.com", "shipped.eml"), ("fay@example.com", "invoice.eml")],
};
foreach ((string to, string file) in sends)
{
    JsonElement sent = await Curl("-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", to, "-T", file, smtp + Domain);
    Console.WriteLine($"app sent {file} to {to}: curl exit {sent.GetProperty("exitCode")}");
}

// The review: every message in the catch-all mailbox, oldest first, read back and checked.
CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "review@example.com", ["mailbox"] = "INBOX",
});
JsonElement[] messages = [.. listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray()];
Console.WriteLine($"list_messages: {messages.Length} message(s)");
int failed = 0;
foreach (JsonElement message in messages)
{
    long uid = message.GetProperty("uid").GetInt64();
    CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "review@example.com", ["mailbox"] = "INBOX", ["uid"] = uid,
    });
    (List<string> problems, string names) = Review(read.StructuredContent!.Value.GetProperty("text").GetString()!);
    Console.WriteLine(problems.Count == 0 ? $"UID {uid}: ok" : $"UID {uid}: FAIL {string.Join("; ", problems)} (fields: {names})");
    failed += problems.Count == 0 ? 0 : 1;
}

return messages.Length > 0 && failed == 0 ? 0 : 1;

// The rules: a From, a Date and a Message-ID field, and every address in the domain.
static (List<string> Problems, string Names) Review(string text)
{
    // The header ends at the first empty line; a line that starts with white space continues the field before it.
    string head = Regex.Replace(text[..text.IndexOf("\r\n\r\n", StringComparison.Ordinal)], @"\r\n[ \t]+", " ");
    var fields = head.Split("\r\n").Select(line => line.Split(':', 2)).Select(pair => (Name: pair[0].Trim(), Value: pair[1].Trim())).ToList();
    var problems = new List<string>();
    string[] missing = [.. new[] { "From", "Date", "Message-ID" }.Where(name => !fields.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))];
    if (missing.Length > 0)
    {
        problems.Add($"no {string.Join(", ", missing)}");
    }

    foreach ((string name, string value) in fields.Where(f => f.Name is "Return-Path" or "From" or "Sender" or "Reply-To" or "To" or "Cc" or "Bcc"))
    {
        foreach (Match address in Regex.Matches(value, @"[^\s<>,;""]+@[^\s<>,;""]+"))
        {
            if (!address.Value.EndsWith("@" + Domain, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{name} {address.Value} is outside {Domain}");
            }
        }
    }

    return (problems, string.Join(", ", fields.Select(f => f.Name)));
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
