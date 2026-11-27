// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds an order confirmation for an address with a typo. seed_mail refuses it as a tool
// error; the agent reads the refusal, picks the mailbox it meant from those it started the server
// with, and seeds that one. The same address over SMTP would have been accepted and lost.
// Run: dotnet run seed-or-correct.cs -- <case>
//   typo        seed for anne@example.com, which has no mailbox (the default)
//   outside     seed for ann@example.org, outside the mail domain
//   malformed   seed for "ann", which is not an address
//   unchanged   seed for anne@example.com, then the mail store's events
//   smtp        send for anne@example.com over SMTP with the curl tool instead
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "typo";

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

// A mail server for example.com, with the mailboxes the agent knows about.
string[] mailboxes = ["ann@example.com", "shop@example.com"];
var arguments = new List<string> { "--mail-domain", "example.com" };
foreach (string mailbox in mailboxes)
{
    arguments.AddRange(["--mailbox", $"{mailbox}:{mailbox.Split('@')[0]}pass"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

string to = which switch { "outside" => "ann@example.org", "malformed" => "ann", _ => "anne@example.com" };
const string message = "From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order 1042 confirmed\r\n\r\nThank you for your order.\r\n";

if (which == "smtp")
{
    // The same mail as an app would send it, over SMTP: accepted, and then?
    CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", "shop@example.com:shoppass", "--mail-from", "shop@example.com", "--mail-rcpt", to,
            "-T", "order.eml", "-w", "%{response_code}", smtp + "shop.example.com" },
    });
    using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
    Console.WriteLine($"curl exit {answer.RootElement.GetProperty("exitCode").GetInt32()}, reply {answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()}");
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["timeout_ms"] = 2000,
    });
    Console.WriteLine($"wait_for_event: {waited.StructuredContent!.Value.GetRawText()}");
    CallToolResult listed = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = to });
    Console.WriteLine($"list_mailboxes {to}: {Text(listed)}");
    return 0;
}

// The seed. A refusal is a tool error whose text names the refusal.
CallToolResult seeded = await Seed(to);
if (seeded.IsError != true)
{
    return 0;
}

string refusal = Regex.Match(Text(seeded), @"was refused: (\w+)\.").Groups[1].Value;
switch (which)
{
    case "unchanged":
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail",
        });
        Console.WriteLine($"mail store changes: {events.StructuredContent!.Value.GetProperty("events").GetArrayLength()}");
        return 0;
    case "typo" when refusal == "NoSuchAccount":
        // The address is in the domain but names no mailbox: the agent picks the one it meant,
        // a mailbox it started the server with whose name the address begins with.
        string meant = mailboxes.Single(m => m.EndsWith("@" + to.Split('@')[1]) && to.StartsWith(m.Split('@')[0]));
        Console.WriteLine($"refusal {refusal}: seeding {meant} instead");
        await Seed(meant);
        CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = meant, ["mailbox"] = "INBOX",
        });
        Console.WriteLine($"{meant} holds {listed.StructuredContent!.Value.GetProperty("messages").GetArrayLength()}");
        return 0;
    default:
        Console.WriteLine($"refusal {refusal}");
        return 0;
}

async Task<CallToolResult> Seed(string address)
{
    CallToolResult result = await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = address, ["message"] = message,
    });
    Console.WriteLine($"seed_mail {address}: {(result.IsError == true ? "tool error, " : "")}{Text(result)}");
    return result;
}

static string Text(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;
