// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves two mail domains, one.test and two.test: surl-mcp runs one server for both,
// curl-mcp's curl tool sends each brand's mail, and the agent shows, mailbox by mailbox, where each
// message landed.
// Run: dotnet run serve-brands.cs -- <case>
//   welcome       each brand sends its welcome to Ann in its own domain (the default)
//   own-password  an IMAP login as ann@two.test with ann@one.test's password
//   bare-name     an IMAP login as ann
//   third         a send to ann@three.test, then the server's answer to its RCPT TO
//   subdomain     a send to ann@mail.one.test
//   unknown       a send to cy@two.test, which has no mailbox
//   both          one send to Bo in both domains
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "welcome";

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

// SMTP and IMAP for two domains: a noreply mailbox per brand, and Ann and Bo in each.
var arguments = new List<string> { "--mail-domain", "one.test", "--mail-domain", "two.test" };
foreach (string mailbox in new[] { "noreply@one.test:onepass", "noreply@two.test:twopass", "ann@one.test:annone", "ann@two.test:anntwo", "bo@one.test:boone", "bo@two.test:botwo" })
{
    arguments.AddRange(["--mailbox", mailbox]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

switch (which)
{
    case "own-password":
        Console.WriteLine(Said(await Curl("-s", "-S", "-u", "ann@two.test:annone", imap + "INBOX?ALL")));
        break;
    case "bare-name":
        Console.WriteLine(Said(await Curl("-s", "-S", "-u", "ann:annone", imap + "INBOX?ALL")));
        break;
    case "third":
        Console.WriteLine(Said(await Send("two", "ann@three.test")));
        await Wait(new() { ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp" });
        CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
        var lines = new List<string>();
        foreach (JsonElement chunk in got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last().GetProperty("chunks").EnumerateArray())
        {
            lines.AddRange(chunk.GetProperty("data").GetString()!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
        }

        int rcpt = lines.FindIndex(l => l.StartsWith("RCPT TO:"));
        Console.WriteLine($"{lines[rcpt]} -> {lines[rcpt + 1]}");
        break;
    case "subdomain":
        Console.WriteLine(Said(await Send("one", "ann@mail.one.test")));
        break;
    case "unknown":
        Console.WriteLine(Said(await Send("two", "cy@two.test")));
        await Wait(new() { ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp" });
        JsonElement none = await Wait(new() { ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = "cy@two.test", ["timeout_ms"] = 2000 });
        Console.WriteLine(none.TryGetProperty("timed_out", out _) ? "no delivery to cy@two.test within 2 s" : "delivered to cy@two.test");
        CallToolResult listed = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "cy@two.test" });
        Console.WriteLine($"list_mailboxes cy@two.test: {listed.Content.OfType<TextContentBlock>().First().Text}");
        break;
    case "both":
        Console.WriteLine(Said(await Send("one", "bo@one.test", "bo@two.test")));
        await Landed("bo@one.test", "bo@two.test");
        break;
    default:
        Console.WriteLine(Said(await Send("one", "ann@one.test")));
        Console.WriteLine(Said(await Send("two", "ann@two.test")));
        await Landed("ann@one.test", "ann@two.test");
        break;
}

return 0;

// A brand's welcome, sent as that brand's noreply mailbox. --crlf makes the fixture's line ends right.
async Task<JsonElement> Send(string brand, params string[] to)
{
    string[] rcpt = to.SelectMany(t => new[] { "--mail-rcpt", t }).ToArray();
    return await Curl(["-s", "-S", "--crlf", "-u", $"noreply@{brand}.test:{brand}pass", "--mail-from", $"noreply@{brand}.test", .. rcpt, "-T", $"{brand}.eml", $"{smtp}{brand}.test"]);
}

// The curl tool runs curl's own arguments and answers in text that is JSON.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// What the curl tool said: its exit code, and its error line when it failed.
string Said(JsonElement answer)
{
    int exit = answer.GetProperty("exitCode").GetInt32();
    return exit == 0 ? "curl exit 0" : $"curl exit {exit}: {answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}";
}

// wait_for_event answers the event, or {"timed_out":true} when its bound, 10 seconds unless given, passes.
async Task<JsonElement> Wait(Dictionary<string, object?> query)
{
    query["server_id"] = id;
    query.TryAdd("timeout_ms", 10000);
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", query);
    return waited.StructuredContent!.Value.Clone();
}

// Waits for a delivery to each owner, then shows, mailbox by mailbox, what landed there.
async Task Landed(params string[] owners)
{
    var delivered = new List<string>();
    long seen = 0;
    while (!owners.All(o => delivered.Any(n => n.StartsWith(o + "/"))))
    {
        JsonElement waited = await Wait(new() { ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["after_sequence"] = seen });
        if (waited.TryGetProperty("timed_out", out _))
        {
            Console.WriteLine("a delivery did not come within 10 s");
            return;
        }

        seen = waited.GetProperty("event").GetProperty("sequence").GetInt64();
        delivered.Add(waited.GetProperty("event").GetProperty("name").GetString()!);
    }

    foreach (string owner in owners)
    {
        CallToolResult mailboxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
        foreach (JsonElement mailbox in mailboxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray())
        {
            string name = mailbox.GetProperty("name").GetString()!;
            int count = mailbox.GetProperty("message_count").GetInt32();
            CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
            {
                ["server_id"] = id, ["owner"] = owner, ["mailbox"] = name, ["uid"] = 1,
            });
            string subject = read.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n").First(l => l.StartsWith("Subject: "));
            Console.WriteLine($"{owner} {name}: {count} message, UID 1 {subject}");
        }
    }
}
