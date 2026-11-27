// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent mails the minutes to four people, one of them outside the server's domain: surl-mcp runs
// the mail server, curl-mcp's curl tool sends with --mail-rcpt-allowfails, get_exchanges shows which
// recipient was refused, and the events and list_messages show who was delivered to.
// Run: dotnet run send-minutes.cs -- <case>
//   allowfails   the four, with --mail-rcpt-allowfails (the default)
//   quiet        the same, and what the curl tool said
//   once         the same, and how many DATA commands the server saw
//   headers      the same, and the To header of Ann's copy
//   without      Dee and Carol, without --mail-rcpt-allowfails
//   all-refused  two outside addresses, with --mail-rcpt-allowfails
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "allowfails";

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

// SMTP for example.com only: the app's mailbox and one for each person on the list inside the domain.
var arguments = new List<string> { "--mail-domain", "example.com" };
foreach (string name in new[] { "app", "ann", "bob", "cy", "dee" })
{
    arguments.AddRange(["--mailbox", $"{name}@example.com:{name}pass"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Who the app names, and whether it carries on past a refused recipient.
(string[] to, bool allowFails) = which switch
{
    "without" => (new[] { "dee@example.com", "carol@elsewhere.org" }, false),
    "all-refused" => (new[] { "carol@elsewhere.org", "dan@elsewhere.org" }, true),
    _ => (new[] { "ann@example.com", "carol@elsewhere.org", "bob@example.com", "cy@example.com" }, true),
};

// The send: -s keeps the progress meter out of the answer, -S keeps the error line in.
string[] rcpt = to.SelectMany(t => new[] { "--mail-rcpt", t }).ToArray();
string[] allow = allowFails ? ["--mail-rcpt-allowfails"] : [];
CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-S", "--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com" }
        .Concat(rcpt).Concat(allow).Concat(["-T", "minutes.eml", smtp + "example.com"]).ToArray(),
});
using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
string error = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!;
Console.WriteLine(exit == 0 ? "curl exit 0" : $"curl exit {exit}: {error.Trim()}");

switch (which)
{
    case "without":
        Console.WriteLine($"dee@example.com holds {await Count("dee@example.com")}");
        return 0;
    case "all-refused":
        return 0;
    case "quiet":
        Console.WriteLine($"standard error: {error.Length} characters");
        return 0;
}

// Delivery is asynchronous: each delivery is a store_changed event; wait for Cy's, the last, with a bound.
var delivered = new List<string>();
long seen = 0;
while (!delivered.Any(n => n.StartsWith("cy@example.com/")))
{
    JsonElement waited = await Wait(new() { ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["after_sequence"] = seen });
    if (waited.TryGetProperty("timed_out", out _))
    {
        Console.WriteLine("no delivery to cy@example.com within 10 s");
        return 1;
    }

    seen = waited.GetProperty("event").GetProperty("sequence").GetInt64();
    delivered.Add(waited.GetProperty("event").GetProperty("name").GetString()!);
}

// The server's record of the exchange, once it has ended: the answer to each RCPT TO.
await Wait(new() { ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp" });
CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
var lines = new List<string>();
foreach (JsonElement chunk in got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last().GetProperty("chunks").EnumerateArray())
{
    lines.AddRange(chunk.GetProperty("data").GetString()!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
}

switch (which)
{
    case "once":
        Console.WriteLine($"DATA commands: {lines.Count(l => l == "DATA")}");
        Console.WriteLine($"deliveries: {delivered.Count}");
        break;
    case "headers":
        CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 1,
        });
        Console.WriteLine(read.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n").First(l => l.StartsWith("To: ")));
        break;
    default:
        // The agent's report: each recipient with the answer to its RCPT TO, and what each mailbox holds.
        for (int i = 0; i < lines.Count - 1; i++)
        {
            Match rcptTo = Regex.Match(lines[i], "^RCPT TO:<(.+)>$");
            if (rcptTo.Success)
            {
                string address = rcptTo.Groups[1].Value;
                string holds = lines[i + 1].StartsWith("250") ? $", holds {await Count(address)}" : "";
                Console.WriteLine($"{address}: {lines[i + 1]}{holds}");
            }
        }

        break;
}

return 0;

// wait_for_event answers the event, or {"timed_out":true} when its bound, 10 seconds, passes.
async Task<JsonElement> Wait(Dictionary<string, object?> query)
{
    query["server_id"] = id;
    query["timeout_ms"] = 10000;
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", query);
    return waited.StructuredContent!.Value.Clone();
}

async Task<int> Count(string owner)
{
    CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX",
    });
    return listed.StructuredContent!.Value.GetProperty("messages").GetArrayLength();
}
