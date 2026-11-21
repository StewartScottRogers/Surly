// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent forwards a role address: surl-mcp runs a mail server whose alias sends support@ to two
// mailboxes, curl-mcp's curl tool mails support@ as a form would, and surl-mcp's events and message
// tools show that both people got it.
// Run: dotnet run forward-mail.cs -- <case>
//   forward        the form mails support@; Ann and Bob each get it (the default)
//   to-field       the To field of the stored copy
//   sender         the sending account's own mailbox
//   one-copy       one message to support@ and ann@ together
//   seed           seed_mail to support@, with no SMTP
//   login          an IMAP login as the alias
//   also-mailbox   an alias that is also a mailbox
//   no-mailbox     an alias whose target is no mailbox
//   outside        an alias whose target is outside the domain
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "forward";

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

// SMTP and IMAP over one mail store for example.com: the form's mailbox, Ann's and Bob's, and one alias.
string alias = which switch
{
    "also-mailbox" => "ann@example.com=bob@example.com",
    "no-mailbox" => "support@example.com=anne@example.com",
    "outside" => "support@example.com=ann@elsewhere.org",
    _ => "support@example.com=ann@example.com,bob@example.com",
};
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com",
        "--mailbox", "form@example.com:formpass", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass",
        "--mail-alias", alias,
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
string imap = server.GetProperty("bound_urls")[1].GetString()!;
long seen = 0;

if (which == "login")
{
    // An alias is an address, not an account: the server has no login for it.
    JsonElement denied = await Curl("-u", "support@example.com:annpass", imap + "INBOX?ALL");
    Console.WriteLine($"login as support@example.com: curl exit {denied.GetProperty("exitCode")}");
    return 0;
}

// The form mails support@ (and, in one-copy, ann@ as well), or the test seeds the store through the alias.
if (which == "seed")
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "support@example.com",
        ["message"] = "From: form@example.com\r\nTo: support@example.com\r\nSubject: Cannot log in\r\n\r\nPlease reset my password.\r\n",
    });
}
else
{
    Console.WriteLine($"support@: curl exit {await Send("support@example.com")}");
}

// Delivery is asynchronous: each delivery is a store_changed event, and the program waits for two, with a bound.
List<string> first = await Delivered(2);
Console.WriteLine($"delivered: {string.Join(", ", first.Order())}");

if (which == "one-copy")
{
    Console.WriteLine($"support@ and ann@: curl exit {await Send("support@example.com", "ann@example.com")}");

    // Deliveries happen in order. A marker for the form, delivered last, shows nothing is still on its way.
    Console.WriteLine($"marker: curl exit {await Send("form@example.com")}");
    List<string> more = await Delivered(3);
    Console.WriteLine($"then delivered: {string.Join(", ", more.Take(2).Order())}, {more[2]}");
}

// The test: what each mailbox holds, read with surl-mcp's message tools.
switch (which)
{
    case "to-field":
        string copy = await Text("ann@example.com", 1);
        Console.WriteLine(copy.Split("\r\n").First(l => l.StartsWith("To: ")));
        Console.WriteLine($"mentions ann or bob: {copy.Contains("ann@example.com") || copy.Contains("bob@example.com")}");
        break;
    case "sender":
        Console.WriteLine($"form@example.com holds {await Count("form@example.com")}");
        break;
    case "one-copy":
        foreach (string owner in new[] { "ann@example.com", "bob@example.com", "form@example.com" })
        {
            Console.WriteLine($"{owner} holds {await Count(owner)}");
        }

        break;
    default:
        foreach (string owner in new[] { "ann@example.com", "bob@example.com" })
        {
            string text = await Text(owner, 1);
            Console.WriteLine($"{owner}: {text.Split("\r\n").First(l => l.StartsWith("Subject: "))["Subject: ".Length..]}");
        }

        break;
}

return 0;

// One mail as the form, with curl's own arguments; the answer is JSON text, and -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

async Task<int> Send(params string[] to)
{
    string[] rcpt = to.SelectMany(t => new[] { "--mail-rcpt", t }).ToArray();
    JsonElement sent = await Curl(["--crlf", "-u", "form@example.com:formpass", "--mail-from", "form@example.com", .. rcpt, "-T", "help.eml", smtp + "example.com"]);
    return sent.GetProperty("exitCode").GetInt32();
}

// Waits, with a bound, for deliveries until this many have come in all, and returns their names in order.
async Task<List<string>> Delivered(int count)
{
    var names = new List<string>();
    while (names.Count < count)
    {
        CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["after_sequence"] = seen, ["timeout_ms"] = 10000,
        });
        JsonElement answer = waited.StructuredContent!.Value;
        if (answer.TryGetProperty("timed_out", out _))
        {
            Console.WriteLine($"only {names.Count} of {count} deliveries within 10 s");
            Environment.Exit(1);
        }

        JsonElement e = answer.GetProperty("event");
        seen = e.GetProperty("sequence").GetInt64();
        names.Add(e.GetProperty("name").GetString()!);
    }

    return names;
}

async Task<int> Count(string owner)
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX",
    });
    return listed.StructuredContent!.Value.GetProperty("messages").GetArrayLength();
}

async Task<string> Text(string owner, long uid)
{
    CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX", ["uid"] = uid,
    });
    return read.StructuredContent!.Value.GetProperty("text").GetString()!;
}
