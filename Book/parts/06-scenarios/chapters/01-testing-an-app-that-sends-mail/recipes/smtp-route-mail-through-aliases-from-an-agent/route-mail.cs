// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests an app that mails support@ and alerts@: surl-mcp runs a mail server whose aliases
// forward both, curl-mcp's curl tool sends as the app would, and surl-mcp's events and store show
// where each message landed.
// Run: dotnet run route-mail.cs -- <case>
//   right          the app mails support@ and alerts@; the test passes (the default)
//   wrong-address  the app mails alert@ instead of alerts@
//   typo-target    the alerts@ alias names opps@, which has no mailbox
#:package ModelContextProtocol.Core@2.4.0

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

// SMTP and IMAP over one mail store for example.com: four mailboxes and two aliases.
string target = which == "typo-target" ? "opps@example.com" : "ops@example.com";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com",
        "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass",
        "--mailbox", "bob@example.com:bobpass", "--mailbox", "ops@example.com:opspass",
        "--mail-alias", "support@example.com=ann@example.com,bob@example.com",
        "--mail-alias", $"alerts@example.com={target}",
    },
});
if (started.IsError == true)
{
    JsonElement refused = started.StructuredContent!.Value;
    Console.WriteLine($"start_server refused: {refused.GetProperty("exit_code")} {refused.GetProperty("exit_code_name")}: {refused.GetProperty("lines")[0]}");
    return 2;
}

JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;

// The app's two sends; after each, the mail store's delivered events say where the message went.
var landed = new List<string>();
long seen = 0;
string alerts = which == "wrong-address" ? "alert@example.com" : "alerts@example.com";
foreach ((string to, string file) in new[] { ("support@example.com", "help.eml"), (alerts, "alert.eml") })
{
    JsonElement sent = await Curl("--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com",
        "--mail-rcpt", to, "-T", file, smtp + "example.com");
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["after_sequence"] = seen,
    });
    var names = new List<string>();
    foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
    {
        names.Add(e.GetProperty("name").GetString()!);
        seen = e.GetProperty("sequence").GetInt64();
    }

    landed.AddRange(names);
    Console.WriteLine($"{to}: curl exit {sent.GetProperty("exitCode")}, delivered to [{string.Join(", ", names)}]");
}

// The test: each target mailbox holds exactly the one message meant for it, read from the store.
var want = new Dictionary<string, string>
{
    ["ann@example.com"] = "Cannot log in", ["bob@example.com"] = "Cannot log in", ["ops@example.com"] = "Disk full",
};
int wrong = 0;
foreach ((string owner, string subject) in want)
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX",
    });
    JsonElement messages = listed.StructuredContent!.Value.GetProperty("messages");
    string got = "nothing";
    if (messages.GetArrayLength() == 1)
    {
        CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX", ["uid"] = messages[0].GetProperty("uid").GetInt64(),
        });
        string text = read.StructuredContent!.Value.GetProperty("text").GetString()!;
        got = text.Split("\r\n").First(line => line.StartsWith("Subject: "))["Subject: ".Length..];
    }

    Console.WriteLine(got == subject ? $"{owner}: {subject}" : $"FAIL: {owner} has {got}, want {subject}");
    wrong += got == subject ? 0 : 1;
}

return wrong == 0 ? 0 : 1;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
