// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent sends mail with curl-mcp's curl tool and confirms the delivery with one wait_for_event
// call, filtered to the recipient's mailbox and to events after those already kept, instead of
// asking the mailbox over and over.
// Run: dotnet run confirm-delivery.cs -- <case>
//   confirm    send to Ann, wait for her delivery, read it (the default)
//   stale      the same, but the wait leaves out after_sequence
//   bob-first  send to Bob, then to Ann; the wait is for Ann
//   never      send to cy@example.com, which has no mailbox
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "confirm";

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

// A mail server for example.com: the shop app's mailbox, Ann's and Bob's.
var arguments = new List<string> { "--mail-domain", "example.com" };
foreach (string person in new[] { "shop", "ann", "bob" })
{
    arguments.AddRange(["--mailbox", $"{person}@example.com:{person}pass"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Earlier in the session: a welcome message already in Ann's INBOX, and its event kept.
await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
{
    ["server_id"] = id, ["to"] = "ann@example.com", ["message"] = "From: shop@example.com\r\nSubject: Welcome\r\n\r\nWelcome to the shop.\r\n",
});

// Before sending: the sequence number of the last event the server has kept.
CallToolResult kept = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id });
long last = kept.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("sequence").GetInt64()).DefaultIfEmpty(0).Max();

// The send, with the curl tool: one per recipient.
string[] recipients = which switch { "bob-first" => ["bob@example.com", "ann@example.com"], "never" => ["cy@example.com"], _ => ["ann@example.com"] };
foreach (string to in recipients)
{
    CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", "shop@example.com:shoppass", "--mail-from", "shop@example.com", "--mail-rcpt", to,
            "-T", "order.eml", smtp + "shop.example.com" },
    });
    using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
    Console.WriteLine($"curl to {to}: exit {answer.RootElement.GetProperty("exitCode").GetInt32()}");
}

// One wait, not a loop: the next change to the recipient's mailbox after the last kept event.
string owner = recipients[^1];
var query = new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = owner,
    ["timeout_ms"] = which == "never" ? 2000 : 10000,
};
if (which != "stale")
{
    query["after_sequence"] = last;
}

CallToolResult waited = await surl.CallToolAsync("wait_for_event", query);
JsonElement result = waited.StructuredContent!.Value;
if (result.TryGetProperty("timed_out", out _))
{
    Console.WriteLine($"no delivery to {owner} within 2 s: {result.GetRawText()}");
    return 1;
}

JsonElement delivered = result.GetProperty("event");
string name = delivered.GetProperty("name").GetString()!;
Console.WriteLine($"event: {delivered.GetProperty("change").GetString()} {name}");

// The event's name is owner/mailbox/;UID=n: enough to read the message that came.
CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX", ["uid"] = int.Parse(name.Split("UID=")[1]),
});
Console.WriteLine(read.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n").First(l => l.StartsWith("Subject: ")));
return 0;
