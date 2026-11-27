// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent walks a large mailbox with list_messages, a page at a time: each page starts after the
// last UID seen, and has_more says when to stop, so no message is missed or seen twice.
// Run: dotnet run walk-mailbox.cs -- <case>
//   walk     60 messages, pages of 25 (the default)
//   exact    50 messages, pages of 25
//   arrives  60 messages; one more is seeded after the first page
//   deleted  60 messages; UID 10, already seen, is deleted over IMAP after the first page
//   limit    a page with a limit of 0
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "walk";

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

// An IMAP server for example.com with Ann's mailbox.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The mailbox under test: one order notice per message.
int count = which == "exact" ? 50 : 60;
for (int order = 1; order <= count; order++)
{
    await Seed(order);
}

if (which == "limit")
{
    try
    {
        await Page(0, 0);
    }
    catch (McpProtocolException refused)
    {
        Console.WriteLine(refused.Message);
    }

    return 0;
}

// The walk: each page asks for the messages after the last UID seen, at most 25 of them.
var seen = new List<long>();
long afterUid = 0;
for (int page = 1; ; page++)
{
    JsonElement answer = await Page(afterUid, 25);
    long[] uids = answer.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("uid").GetInt64()).ToArray();
    bool hasMore = answer.GetProperty("has_more").GetBoolean();
    Console.WriteLine($"page {page}: {uids.Length} messages, UIDs {uids[0]} to {uids[^1]}, has_more {hasMore.ToString().ToLowerInvariant()}");
    seen.AddRange(uids);
    if (!hasMore)
    {
        break;
    }

    afterUid = uids[^1];
    if (page == 1 && which == "arrives")
    {
        await Seed(count + 1);
    }
    else if (page == 1 && which == "deleted")
    {
        // Ann's mail client deletes UID 10 over IMAP: flag it \Deleted, then expunge.
        foreach (string command in new[] { "UID STORE 10 +FLAGS (\\Deleted)", "EXPUNGE" })
        {
            await curl.CallToolAsync("curl", new Dictionary<string, object?>
            {
                ["args"] = new[] { "-s", "-S", "-u", "ann@example.com:annpass", "-X", command, imap + "INBOX" },
            });
        }
    }
}

bool once = seen.Distinct().Count() == seen.Count && seen.SequenceEqual(seen.Order());
Console.WriteLine($"{seen.Count} messages, {(once ? "each once, in UID order" : "with repeats or out of order")}");
if (which == "deleted")
{
    // Counting by position instead: the 26th message is no longer UID 26.
    JsonElement all = await Page(0, 1000);
    Console.WriteLine($"by position, the 26th message is now UID {all.GetProperty("messages")[25].GetProperty("uid").GetInt64()}");
}

return 0;

async Task<JsonElement> Page(long after, int limit)
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["after_uid"] = after, ["limit"] = limit,
    });
    return listed.StructuredContent!.Value.Clone();
}

async Task Seed(int order) => await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
{
    ["server_id"] = id, ["to"] = "ann@example.com",
    ["message"] = $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {order} shipped\r\n\r\nOrder {order} left the warehouse.\r\n",
});
