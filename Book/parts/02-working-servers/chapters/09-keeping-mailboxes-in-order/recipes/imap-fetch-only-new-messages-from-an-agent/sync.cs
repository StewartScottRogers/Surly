// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent that fetches only new mail: it remembers the highest UID it has read, asks for the UIDs
// above it with UID SEARCH through curl-mcp's curl tool, and fetches those; surl-mcp's record shows
// which messages each pass fetched.
// Run: dotnet run sync.cs
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// IMAP on a free port, example.com, one mailbox.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
const string Ann = "ann@example.com:annpass";
long lastSequence = 0;

await Seed(1);
await Seed(2);
long seen = await Pass("pass 1", 0);

// Two more orders between passes; list_messages with after_uid says what the next pass should fetch.
await Seed(3);
await Seed(4);
CallToolResult newer = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["after_uid"] = seen,
});
Console.WriteLine($"list_messages after_uid {seen}: {string.Join(" ", newer.StructuredContent!.Value.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("uid")))}");
seen = await Pass("pass 2", seen);
seen = await Pass("pass 3", seen);

// UIDNEXT, and UIDVALIDITY against list_mailboxes' uid_validity.
string status = Out(await Curl("-u", Ann, "-X", "STATUS INBOX (UIDNEXT UIDVALIDITY)", imap));
CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
JsonElement inbox = boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "INBOX");
Console.WriteLine($"STATUS: UIDNEXT {status.Split(' ')[4]}; UIDVALIDITY equals uid_validity: {status.Contains($"UIDVALIDITY {inbox.GetProperty("uid_validity")})")}");

// Another client deletes message 1; a fifth order arrives. UIDs are not reused; message numbers shift.
await Curl("-o", "store1.txt", "-u", Ann, "-X", @"STORE 1 +FLAGS (\Deleted)", imap + "INBOX");
Console.WriteLine($"EXPUNGE: {Out(await Curl("-u", Ann, "-X", "EXPUNGE", imap + "INBOX"))}");
await Seed(5);
seen = await Pass("pass 4", seen);
Console.WriteLine($"URL query INBOX?UID%205:*: {Out(await Curl("-u", Ann, imap + "INBOX?UID%205:*"))}");
JsonElement gone = await Curl("-u", Ann, imap + "INBOX;UID=1");
Console.WriteLine($"UID 1: curl exit {gone.GetProperty("exitCode")}");
return 0;

// One pass: UID SEARCH above the remembered UID, keep only UIDs above it (* is the largest in use), fetch each.
async Task<long> Pass(string name, long lastUid)
{
    long lastExchange = await LastExchangeId();
    string answer = Out(await Curl("-u", Ann, "-X", $"UID SEARCH UID {lastUid + 1}:*", imap + "INBOX"));
    long[] fresh = answer.Split(' ').Skip(2).Select(long.Parse).Where(u => u > lastUid).ToArray();
    foreach (long uid in fresh)
    {
        await Curl("-o", $"message{uid}.eml", "-u", Ann, imap + $"INBOX;UID={uid}");
    }

    // Each curl call is one exchange: wait, at most 10 s for each, until surl has ended every one it started, then read what it received.
    while (await Count("exchange_ended") < await Count("exchange_started"))
    {
        CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
        });
        lastSequence = waited.StructuredContent!.Value.GetProperty("event").GetProperty("sequence").GetInt64();
    }

    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["after_exchange_id"] = lastExchange,
    });
    string asked = string.Join(" | ", recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(x => x.GetProperty("chunks").EnumerateArray())
        .Where(c => c.GetProperty("direction").GetString() == "received")
        .SelectMany(c => c.GetProperty("data").GetString()!.Split("\r\n"))
        .Where(l => l.Contains(" UID FETCH ")).Select(l => l[(l.IndexOf(' ') + 1)..]));
    long last = fresh.Length > 0 ? fresh.Max() : lastUid;
    Console.WriteLine($"{name} (after UID {lastUid}): {answer}; fetched {(fresh.Length > 0 ? string.Join(" ", fresh) : "none")}; last UID {last}");
    Console.WriteLine($"{name}, surl received: {(asked.Length > 0 ? asked : "no UID FETCH")}");
    return last;
}

async Task<int> Count(string kind)
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { kind }, ["max_events"] = 500,
    });
    return events.StructuredContent!.Value.GetProperty("events").GetArrayLength();
}

async Task<long> LastExchangeId()
{
    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["max_exchanges"] = 100 });
    return recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .Select(x => x.GetProperty("exchange_id").GetInt64()).DefaultIfEmpty(0).Max();
}

// seed_mail delivers one order; wait_for_event answers its store_changed event, or timed_out after 10 s.
async Task Seed(int n)
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "ann@example.com",
        ["message"] = $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {n} shipped\r\n\r\nOn its way.\r\n",
    });
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail",
        ["name"] = $"ann@example.com/INBOX/;UID={n}", ["timeout_ms"] = 10000,
    });
    if (!waited.StructuredContent!.Value.TryGetProperty("event", out _))
    {
        throw new TimeoutException($"order {n} never arrived");
    }
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
