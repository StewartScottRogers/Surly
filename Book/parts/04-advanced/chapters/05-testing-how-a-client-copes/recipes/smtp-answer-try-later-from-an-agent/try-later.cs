// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests what a mailer does when the server answers a recipient command 451, try later:
// surl-mcp starts a mail server whose first RCPT (or first two) get that temporary error, curl-mcp's
// curl tool sends as the app would, and surl-mcp's tools show what the server applied and stored.
// Run: dotnet run try-later.cs -- <case>
//   retry       the first RCPT is answered 451; the same send again is accepted
//   received    the same, and the EHLO name in the stored message's Received line
//   events      the same, and the events and the wire: one fault applied, one delivery, the 451 on the wire
//   abandon     two recipients, the first RCPT refused (one of two answers): the message is abandoned
//   allowfails  the same send with --mail-rcpt-allowfails (the second answer): bob alone gets it
//   spent       then a plain send to ann, the rule spent
//   bad-reply   a rule whose reply is a code with no text
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "retry";
bool twice = mode is "abandon" or "allowfails" or "spent";

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

// SMTP on a free port for example.com, a mailbox for the app, Ann and Bob, and one fault rule on RCPT.
string reply = mode == "bad-reply" ? "451" : "451 4.3.0 try later";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com",
        "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass",
        "--fault", $"error;scheme=smtp;command=RCPT;reply={reply};times={(twice ? 2 : 1)}",
    },
});
if (started.IsError == true)
{
    JsonElement refusal = started.StructuredContent!.Value;
    Console.WriteLine($"start refused: {refusal.GetProperty("exit_code")} {refusal.GetProperty("exit_code_name")}: {refusal.GetProperty("lines")[0]}");
    return 0;
}

string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The app: one message, logged in as app@example.com; the EHLO name is the URL's path.
long seen = 0;
async Task<bool> Send(string ehlo, string[] to, bool allowFails = false)
{
    var arguments = new List<string> { "--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com" };
    arguments.AddRange(to.SelectMany(address => new[] { "--mail-rcpt", address }));
    if (allowFails)
    {
        arguments.Add("--mail-rcpt-allowfails");
    }

    arguments.AddRange(["-T", "order.eml", "-w", "%{response_code}", smtp + ehlo]);
    JsonElement sent = await Curl(arguments.ToArray());
    int exit = sent.GetProperty("exitCode").GetInt32();
    Console.WriteLine($"{ehlo}: curl exit {exit}, reply {sent.GetProperty("standardOutput").GetProperty("content").GetString()}");

    // Delivery is asynchronous: an accepted send is a delivery event, and the wait for it has a bound.
    if (exit != 0)
    {
        return true;
    }

    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["after_sequence"] = seen, ["timeout_ms"] = 10000,
    });
    JsonElement answer = waited.StructuredContent!.Value;
    if (answer.TryGetProperty("timed_out", out _))
    {
        return false;
    }

    seen = answer.GetProperty("event").GetProperty("sequence").GetInt64();
    return true;
}

string[] ann = ["ann@example.com"];
string[] both = ["ann@example.com", "bob@example.com"];
bool delivered = true;
switch (mode)
{
    case "abandon":
        await Send("first", both);
        break;
    case "allowfails":
        await Send("first", both);
        delivered = await Send("second", both, allowFails: true);
        break;
    case "spent":
        await Send("first", both);
        await Send("second", both, allowFails: true);
        delivered = await Send("third", ann);
        break;
    default:
        await Send("first", ann);
        delivered = await Send("second", ann);
        break;
}

if (!delivered)
{
    Console.WriteLine("no delivery within 10 s");
    return 1;
}

// The test: the rule and the store, read without a client.
CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = id });
JsonElement rule = faults.StructuredContent!.Value.GetProperty("faults")[0];
Console.WriteLine($"list_faults: {rule.GetProperty("fault_id")} remaining {rule.GetProperty("remaining")}");
foreach (string owner in both)
{
    if (twice || owner == "ann@example.com")
    {
        CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
        Console.WriteLine($"list_mailboxes: {owner} holds {boxes.StructuredContent!.Value.GetProperty("mailboxes")[0].GetProperty("message_count")}");
    }
}

if (mode == "received")
{
    CallToolResult message = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 1,
    });
    string text = message.StructuredContent!.Value.GetProperty("text").GetString()!;
    string received = text.Split("\r\n").First(line => line.StartsWith("Received: "));
    Console.WriteLine(received[..received.IndexOf(" (")]);
}

if (mode == "events")
{
    CallToolResult applied = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "fault_applied" } });
    JsonElement[] appliedEvents = applied.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToArray();
    Console.WriteLine($"get_events: {appliedEvents.Length} fault_applied, rule {appliedEvents[0].GetProperty("rule_id")}");
    CallToolResult stored = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "store_changed" } });
    Console.WriteLine($"get_events: {stored.StructuredContent!.Value.GetProperty("events").GetArrayLength()} store_changed");
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
    string wire = string.Concat(exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(exchange => exchange.GetProperty("chunks").EnumerateArray())
        .Where(chunk => chunk.GetProperty("direction").GetString() == "sent")
        .Select(chunk => chunk.GetProperty("data").GetString()));
    Console.WriteLine($"get_exchanges: {wire.Split("451 4.3.0 try later").Length - 1} 451 sent");
}

return 0;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
