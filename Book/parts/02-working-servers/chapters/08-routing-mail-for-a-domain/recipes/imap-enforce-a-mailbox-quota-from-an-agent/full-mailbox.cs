// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fills a mailbox to its quota through curl-mcp's curl tool and explains the refusal it then
// gets from what surl-mcp recorded: the exchange, the store and the events.
// Run: dotnet run full-mailbox.cs [-- zero]
//   (no argument)  fill the mailbox, send again, then find what frees room
//   zero           start a server whose quota is 0
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fill";

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

// SMTP and IMAP on free ports; ann's mailboxes hold at most 1k, bob has no quota.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com",
        "--mailbox", which == "zero" ? "ann@example.com:annpass;quota=0" : "ann@example.com:annpass;quota=1k",
        "--mailbox", "bob@example.com:bobpass", "--mailbox", "shop@example.com:shoppass",
    },
});
if (started.IsError == true)
{
    JsonElement refused = started.StructuredContent!.Value;
    Console.WriteLine($"start_server: tool error, exit_code {refused.GetProperty("exit_code")} ({refused.GetProperty("exit_code_name").GetString()}), {refused.GetProperty("lines")[0].GetString()}");
    return 1;
}

string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;
const string Ann = "ann@example.com:annpass";
long lastSequence = 0;

Console.WriteLine($"send 1: {await Send("ann@example.com")}");
Console.WriteLine($"send 2: {await Send("ann@example.com")}");
Console.WriteLine($"send 3: {await Send("ann@example.com")}");

// Why: what surl sent from the recipient on, in the last SMTP exchange it recorded.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
JsonElement third = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last();
string sent = string.Concat(third.GetProperty("chunks").EnumerateArray()
    .Where(c => c.GetProperty("direction").GetString() == "sent").Select(c => c.GetProperty("data").GetString()));
string[] replies = sent.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
Console.WriteLine($"replies from RCPT on: {string.Join(" | ", replies.SkipWhile(r => !r.StartsWith("250 2.1.5")).TakeWhile(r => !r.StartsWith("221")))}");

Console.WriteLine($"INBOX: {await Count("ann@example.com")} messages");
CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX" });
string sizes = string.Join(", ", listed.StructuredContent!.Value.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("size")));
Console.WriteLine($"stored sizes: {sizes} of {new FileInfo("newsletter.eml").Length} sent");

// The store's own delivery obeys the quota too.
CallToolResult seeded = await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
{
    ["server_id"] = id, ["to"] = "ann@example.com", ["message"] = File.ReadAllText("newsletter.eml"),
});
Console.WriteLine($"seed_mail: isError {seeded.IsError}, {seeded.Content.OfType<TextContentBlock>().Single().Text}");

// IMAP APPEND into the full mailbox: curl exits 25, and -v shows the server's line.
JsonElement appended = await Curl("-v", "-u", Ann, "-T", "note.eml", imap + "INBOX");
string no = appended.GetProperty("standardError").GetProperty("content").GetString()!.Split('\n').Select(l => l.Trim()).First(l => l.Contains(" NO "));
Console.WriteLine($"APPEND: curl exit {appended.GetProperty("exitCode")}, {no}");

// One full mailbox holds the message back from every recipient; bob alone has room.
Console.WriteLine($"bob and ann: {await Send("bob@example.com", "ann@example.com")}; bob's INBOX: {await Count("bob@example.com")} messages");
Console.WriteLine($"bob alone: {await Send("bob@example.com")}");

// \Deleted still counts; EXPUNGE frees the room.
Console.WriteLine($"STORE 1 +FLAGS (\\Deleted): {Out(await Curl("-u", Ann, "-X", @"STORE 1 +FLAGS (\Deleted)", imap + "INBOX"))}");
Console.WriteLine($"send with UID 1 deleted: {await Send("ann@example.com")}");
Console.WriteLine($"EXPUNGE: {Out(await Curl("-u", Ann, "-X", "EXPUNGE", imap + "INBOX"))}");
Console.WriteLine($"send after EXPUNGE: {await Send("ann@example.com")}");
return 0;

// The app's send, as the shop, through the curl tool; then a bounded wait until surl has ended the exchange.
async Task<string> Send(params string[] recipients)
{
    var arguments = new List<string> { "-w", "%{response_code}", "-u", "shop@example.com:shoppass", "--mail-from", "shop@example.com" };
    foreach (string to in recipients)
    {
        arguments.AddRange(["--mail-rcpt", to]);
    }

    arguments.AddRange(["-T", "newsletter.eml", smtp + "example.com"]);
    JsonElement answer = await Curl([.. arguments]);
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp",
        ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
    });
    if (waited.StructuredContent!.Value.TryGetProperty("event", out JsonElement ended))
    {
        lastSequence = ended.GetProperty("sequence").GetInt64();
    }

    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    return $"curl exit {answer.GetProperty("exitCode")}, {Out(answer)}{(error.Length > 0 ? ", " + error : "")}";
}

async Task<int> Count(string owner)
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
    return boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray()
        .Single(m => m.GetProperty("name").GetString() == "INBOX").GetProperty("message_count").GetInt32();
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
