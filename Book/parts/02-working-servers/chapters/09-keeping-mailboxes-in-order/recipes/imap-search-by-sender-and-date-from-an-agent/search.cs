// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent searches a mailbox by sender and date over IMAP through curl-mcp: a query on an IMAP URL
// is a SEARCH. The messages come from surl-mcp's seed_mail.
// Run: dotnet run search.cs [-- <case>]
//   (no argument)  FROM shop and SENTSINCE 01-Jun-2026, then fetch the matches by UID
//   substring      FROM in two letter cases
//   name           FROM with a display name
//   since          SINCE a date in the past
//   future         SINCE a date in the future
//   sent           SENTSINCE and SENTBEFORE
//   combine        several criteria in one search
//   or             OR
//   nothing        a search that matches nothing
//   bad-date       a date in the wrong format
//   renumber       the same search after an expunge
//   uid            UID SEARCH, then a fetch of its first UID
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "solution";

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

// Four messages with a From and a Date header, each waited for, with a bound, by its delivery event.
string[] mails =
[
    "From: shop@example.com\r\nDate: Mon, 02 Mar 2026 10:00:00 +0000\r\nSubject: Order 1\r\n\r\nFirst.\r\n",
    "From: bank@example.org\r\nDate: Wed, 15 Apr 2026 10:00:00 +0000\r\nSubject: Statement\r\n\r\nYour statement.\r\n",
    "From: shop@example.com\r\nDate: Fri, 10 Jul 2026 10:00:00 +0000\r\nSubject: Order 2\r\n\r\nSecond.\r\n",
    "From: Shop Team <shop@example.com>\r\nDate: Tue, 03 Nov 2026 10:00:00 +0000\r\nSubject: Order 3\r\n\r\nThird.\r\n",
];
for (int n = 1; n <= mails.Length; n++)
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "ann@example.com", ["message"] = "To: ann@example.com\r\n" + mails[n - 1],
    });
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail",
        ["name"] = $"ann@example.com/INBOX/;UID={n}", ["timeout_ms"] = 10000,
    });
    if (!waited.StructuredContent!.Value.TryGetProperty("event", out _))
    {
        Console.WriteLine($"message {n} never arrived");
        return 1;
    }
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// A search is the query of the folder's URL; %20 is a space.
async Task<string> Search(string criteria) => Out(await Curl("-u", Ann, imap + "INBOX?" + criteria.Replace(" ", "%20")));

// The headers of one message by UID, on one line.
async Task<string> Header(int uid) =>
    Out(await Curl("-u", Ann, imap + $"INBOX;UID={uid};SECTION=HEADER.FIELDS%20(FROM%20SUBJECT)")).Replace("\r\n", " | ");

// One IMAP command with -X on INBOX; the folder is selected first.
Task<JsonElement> Command(string text) => Curl("-u", Ann, "-X", text, imap + "INBOX");

switch (mode)
{
    case "solution":
        Console.WriteLine($"search: {await Search("FROM shop SENTSINCE 01-Jun-2026")}");
        Console.WriteLine($"UID 3: {await Header(3)}");
        Console.WriteLine($"UID 4: {await Header(4)}");
        break;

    case "substring":
        Console.WriteLine($"shop: {await Search("FROM shop")}");
        Console.WriteLine($"SHOP: {await Search("FROM SHOP")}");
        break;

    case "name":
        Console.WriteLine(await Search("FROM \"Shop Team\""));
        break;

    case "since":
        Console.WriteLine(await Search("SINCE 01-Jan-2020"));
        break;

    case "future":
        Console.WriteLine(await Search("SINCE 01-Jan-2099"));
        break;

    case "sent":
        Console.WriteLine($"SENTSINCE: {await Search("SENTSINCE 01-Jul-2026")}");
        Console.WriteLine($"SENTBEFORE: {await Search("SENTBEFORE 01-Jul-2026")}");
        break;

    case "combine":
        Console.WriteLine(await Search("FROM shop SENTSINCE 01-Jun-2026 SENTBEFORE 01-Oct-2026"));
        break;

    case "or":
        Console.WriteLine(await Search("OR FROM bank FROM Team"));
        break;

    case "nothing":
        Console.WriteLine(await Search("SINCE 01-Jan-2020 FROM nobody"));
        break;

    case "bad-date":
    {
        // -v adds the server's replies to stderr; the refusal is the tagged BAD line.
        JsonElement failed = await Curl("-v", "-u", Ann, imap + "INBOX?SENTSINCE%202026-11-03");
        string reply = failed.GetProperty("standardError").GetProperty("content").GetString()!
            .Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("< A00") && l.Contains(" BAD "));
        Console.WriteLine($"SENTSINCE 2026-11-03: curl exit {failed.GetProperty("exitCode")}, {reply}");
        break;
    }

    case "renumber":
        Console.WriteLine($"before: {await Search("FROM shop")}");
        await Command(@"STORE 1 +FLAGS (\Deleted)");
        await Command("EXPUNGE");
        Console.WriteLine($"after expunging 1: {await Search("FROM shop")}");
        break;

    case "uid":
        await Command(@"STORE 1 +FLAGS (\Deleted)");
        await Command("EXPUNGE");
        Console.WriteLine($"UID SEARCH: {Out(await Command("UID SEARCH FROM shop"))}");
        Console.WriteLine($"UID 3: {Out(await Curl("-u", Ann, imap + "INBOX;UID=3;SECTION=HEADER.FIELDS%20(SUBJECT)"))}");
        break;
}

return 0;
