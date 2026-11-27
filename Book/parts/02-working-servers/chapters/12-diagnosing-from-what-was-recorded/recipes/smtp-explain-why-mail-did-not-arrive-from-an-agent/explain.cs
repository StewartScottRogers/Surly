// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent explains why mail an app sent never arrived: get_exchanges shows the server accepted the
// recipient and the message, list_mailboxes shows no mailbox has that address, and no delivery
// event came, so the server discarded it.
// Run: dotnet run explain.cs [-- <case>]
//   (no argument)  the app sends to anne@example.com, a typo; the agent explains
//   dialogue       the same send, then the recipient's and the message's replies from get_exchanges
//   mailboxes      the same send, then list_mailboxes for the typo and for Ann
//   events         the same send, then the store_changed events the server kept
//   outside        the app's typo is outside the domain, ann@example.cm
//   catch-all      the server is started with --mail-catch-all; the same typo
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "explain";
string recipient = which == "outside" ? "ann@example.cm" : "anne@example.com";

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

// SMTP for example.com: the app's mailbox and Ann's.
var options = new List<string> { "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass" };
if (which == "catch-all")
{
    options.AddRange(["--mailbox", "postmaster@example.com:pmpass", "--mail-catch-all", "example.com=postmaster@example.com"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" }, ["arguments"] = options,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The app's send, as the app makes it.
CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-S", "--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", recipient, "-T", "welcome.eml", smtp + "shop.example.com" },
});
using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
Console.WriteLine($"the app: curl exit {answer.RootElement.GetProperty("exitCode").GetInt32()}");

// The record is complete once the exchange has ended; then a bounded wait for the delivery.
await Surl("wait_for_event", new() { ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp", ["timeout_ms"] = 10000 });
JsonElement waited = (await Surl("wait_for_event", new() { ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["timeout_ms"] = 2000 })).StructuredContent!.Value;
List<(string Who, string Line)> dialogue = await Dialogue();
string Reply(string command) => dialogue[dialogue.FindIndex(l => l.Who == "C" && l.Line.StartsWith(command)) + 1].Line;

switch (which)
{
    case "dialogue":
        Console.WriteLine($"RCPT TO:<{recipient}> -> {Reply("RCPT TO")}");
        Console.WriteLine($". -> {Reply(".")}");
        return 0;
    case "mailboxes":
        foreach (string owner in new[] { recipient, "ann@example.com" })
        {
            CallToolResult boxes = await Surl("list_mailboxes", new() { ["owner"] = owner });
            Console.WriteLine(boxes.IsError == true
                ? $"{owner}: tool error: {boxes.Content.OfType<TextContentBlock>().Single().Text}"
                : $"{owner}: {string.Join(", ", boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Select(b => b.GetProperty("name").GetString()))}");
        }

        return 0;
    case "events":
        JsonElement kept = (await Surl("get_events", new() { ["kinds"] = new[] { "store_changed" }, ["store"] = "mail" })).StructuredContent!.Value;
        Console.WriteLine($"store_changed events: {kept.GetProperty("events").GetArrayLength()}");
        return 0;
    case "outside":
        Console.WriteLine($"RCPT TO:<{recipient}> -> {Reply("RCPT TO")}");
        return 0;
    case "catch-all":
        JsonElement caught = waited.GetProperty("event");
        Console.WriteLine($"{caught.GetProperty("change").GetString()} {caught.GetProperty("name").GetString()}");
        return 0;
}

// The agent's explanation, from what the server recorded.
bool delivered = !waited.TryGetProperty("timed_out", out _);
bool recipientTaken = Reply("RCPT TO").StartsWith("250");
bool messageTaken = Reply(".").StartsWith("250");
bool hasMailbox = (await Surl("list_mailboxes", new() { ["owner"] = recipient })).IsError != true;
Console.WriteLine($"recipient accepted: {recipientTaken}; message accepted: {messageTaken}; delivered: {delivered}; {recipient} has a mailbox: {hasMailbox}");
if (recipientTaken && messageTaken && !delivered && !hasMailbox)
{
    Console.WriteLine($"explanation: the server took the mail for {recipient}, an address in its domain with no mailbox, and discarded it");
}

return 0;

async Task<CallToolResult> Surl(string tool, Dictionary<string, object?> arguments)
{
    arguments["server_id"] = id;
    return await surl.CallToolAsync(tool, arguments);
}

// The last SMTP exchange from get_exchanges, a line per protocol line: C what the client sent, S what the server sent.
async Task<List<(string Who, string Line)>> Dialogue()
{
    JsonElement exchange = (await Surl("get_exchanges", new() { ["scheme"] = "smtp" })).StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last();
    var lines = new List<(string, string)>();
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        string who = chunk.GetProperty("direction").GetString() == "received" ? "C" : "S";
        string data = chunk.GetProperty("data").GetString()!;
        foreach (string line in (data.EndsWith("\r\n") ? data[..^2] : data).Split("\r\n"))
        {
            lines.Add((who, line));
        }
    }

    return lines;
}
