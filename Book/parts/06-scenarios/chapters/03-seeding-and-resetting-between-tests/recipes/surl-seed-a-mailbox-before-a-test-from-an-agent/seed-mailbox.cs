// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds ten messages into a mailbox with surl-mcp before the client connects, lets the
// client (curl-mcp's curl tool) read one over IMAP and delete one over POP3, and checks the result.
// Run: dotnet run seed-mailbox.cs -- [<address>]
//   (no argument)  seed ann@example.com and run the client (the default)
//   <address>      seed another address instead, such as bob@example.com
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string to = args.Length > 0 ? args[0] : "ann@example.com";

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

// The server: SMTP, IMAP and POP3 over one mail store, with one mailbox.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "pop3://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:secret" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string[] urls = started.StructuredContent!.Value.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();

// The seed: ten messages, delivered as SMTP would deliver them, before any client connects.
for (int i = 1; i <= 10; i++)
{
    CallToolResult seeded = await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = to,
        ["message"] = $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {i:00}\r\n\r\nOrder {i:00} is ready.\r\n",
    });
    if (seeded.IsError == true)
    {
        Console.WriteLine(seeded.Content.OfType<TextContentBlock>().Single().Text);
        Console.WriteLine("tool error: yes");
        return 1;
    }
}

JsonElement inbox = await Inbox();
Console.WriteLine($"seeded: {inbox.GetProperty("message_count")} messages, {inbox.GetProperty("unseen_count")} unseen");

// The client under test: it reads UID 3 over IMAP, then deletes message 1 over POP3.
string message = await Curl(["-sS", "-u", "ann@example.com:secret", urls[1] + "INBOX;UID=3"]);
Console.WriteLine($"the client read: {message.Split('\n').First(l => l.StartsWith("Subject:")).TrimEnd()}");
await Curl(["-sS", "-u", "ann@example.com:secret", "-X", "DELE 1", urls[2]]);
Console.WriteLine("the client sent DELE 1");

// After: the store as the client left it, read from the server's side.
inbox = await Inbox();
Console.WriteLine($"after: {inbox.GetProperty("message_count")} messages, {inbox.GetProperty("unseen_count")} unseen");
CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
});
JsonElement[] left = listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray().ToArray();
Console.WriteLine($"seen: UID {string.Join(" ", left.Where(m => m.GetProperty("flags").EnumerateArray().Any(f => f.GetString() == @"\Seen")).Select(m => m.GetProperty("uid")))}");
Console.WriteLine($"left: UID {string.Join(" ", left.Select(m => m.GetProperty("uid")))}");
return 0;

// ann's INBOX, as list_mailboxes answers it.
async Task<JsonElement> Inbox()
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
    return boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Single(b => b.GetProperty("name").GetString() == "INBOX").Clone();
}

// One curl command line; curl-mcp answers in text, as JSON.
async Task<string> Curl(string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
}
