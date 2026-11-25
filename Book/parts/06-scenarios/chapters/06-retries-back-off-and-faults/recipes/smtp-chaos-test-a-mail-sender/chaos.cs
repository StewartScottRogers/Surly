// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent chaos-tests a mail sender: surl-mcp runs the mail server and adds a delay, a dropped
// connection and a temporary error to it; the sender, curl with retries, sends three orders; the
// agent then counts each order's Message-ID in the mailbox to prove each arrived exactly once.
// Run: dotnet run chaos.cs [-- <case>]
//   (no argument)  three faults, three orders, each counted
//   faults         the same, then the fault_applied events the server kept
//   no-retry       the sender without retries meets the temporary error
//   lost-reply     the connection is cut inside the reply that confirms a stored message
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "chaos";

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

// SMTP for example.com: the shop's mailbox and Ann's.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "shop@example.com:shoppass", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string[] retries = ["--retry", "3", "--retry-all-errors", "--retry-delay", "1"];

if (which == "no-retry")
{
    await AddFault("error;scheme=smtp;command=RCPT;reply=451 4.3.0 try later;times=1");
    await Send(1, []);
    await Count(1);
    return 0;
}

if (which == "lost-reply")
{
    // A clean send first; its record gives the bytes the server sends up to the 354 that asks for the message.
    await Send(1, retries);
    long upTo354 = 0;
    foreach (JsonElement chunk in (await Surl("get_exchanges", new() { ["scheme"] = "smtp" })).StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray())
    {
        if (chunk.GetProperty("direction").GetString() == "sent")
        {
            string data = chunk.GetProperty("data").GetString()!;
            upTo354 += Encoding.UTF8.GetByteCount(data);
            if (data.StartsWith("354"))
            {
                break;
            }
        }
    }

    // Cut the next connection one byte into the reply after the message: stored, but not confirmed.
    await AddFault($"drop;scheme=smtp;after-bytes={upTo354 + 1};times=1", close: "graceful");
    await Send(2, retries);
    int twice = await Count(2);
    return twice == 0 ? 0 : 1;
}

// The chaos: the next MAIL held 2 seconds, the next DATA's connection dropped, the next RCPT answered 451.
await AddFault("delay;scheme=smtp;stage=command;command=MAIL;ms=2000;times=1");
await AddFault("drop;scheme=smtp;stage=command;command=DATA;times=1");
await AddFault("error;scheme=smtp;command=RCPT;reply=451 4.3.0 try later;times=1");

for (int order = 1; order <= 3; order++)
{
    await Send(order, retries);
}

if (which == "faults")
{
    JsonElement events = (await Surl("get_events", new() { ["kinds"] = new[] { "fault_applied" } })).StructuredContent!.Value.GetProperty("events");
    Console.WriteLine($"faults applied: {string.Join(", ", events.EnumerateArray().Select(e => $"{e.GetProperty("rule_id").GetString()} {e.GetProperty("fault_kind").GetString()}"))}");
    JsonElement left = (await Surl("list_faults", new())).StructuredContent!.Value.GetProperty("faults");
    Console.WriteLine($"remaining: {string.Join(", ", left.EnumerateArray().Select(f => $"{f.GetProperty("fault_id").GetString()} {f.GetProperty("remaining").GetRawText()}"))}");
    return 0;
}

int wrong = await Count(3);
Console.WriteLine(wrong == 0 ? "every order arrived exactly once" : $"{wrong} order(s) did not arrive exactly once");
return wrong == 0 ? 0 : 1;

// The sender: curl, with or without retries, sending order<n>.eml to Ann.
async Task Send(int order, string[] retry)
{
    string[] sendArgs = ["-s", "-S", "--crlf", "-m", "10", "-u", "shop@example.com:shoppass", "--mail-from", "shop@example.com", "--mail-rcpt", "ann@example.com",
        .. retry, "-T", $"order{order}.eml", "-w", "%{response_code} %{num_retries}", smtp + "shop.example.com"];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = sendArgs });
    using JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    string[] written = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Split(' ');
    Console.WriteLine($"order {order}: curl exit {answer.RootElement.GetProperty("exitCode").GetInt32()}, reply {written[0]}, retries {written[1]}");
}

// The proof: how many messages in Ann's INBOX carry each order's Message-ID. Answers how many orders were not there exactly once.
async Task<int> Count(int orders)
{
    var ids = new List<string>();
    foreach (JsonElement message in (await Surl("list_messages", new() { ["owner"] = "ann@example.com", ["mailbox"] = "INBOX" })).StructuredContent!.Value.GetProperty("messages").EnumerateArray())
    {
        string text = (await Surl("read_message", new() { ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = message.GetProperty("uid").GetInt64() }))
            .StructuredContent!.Value.GetProperty("text").GetString()!;
        ids.Add(Regex.Match(text, @"^Message-ID: (\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value);
    }

    int wrong = 0;
    for (int order = 1; order <= orders; order++)
    {
        int n = ids.Count(i => i == $"<order-{order}@shop.example.com>");
        Console.WriteLine($"<order-{order}@shop.example.com> stored {n} time(s)");
        wrong += n == 1 ? 0 : 1;
    }

    return wrong;
}

async Task AddFault(string fault, string? close = null)
{
    var arguments = new Dictionary<string, object?> { ["fault"] = fault };
    if (close is not null)
    {
        arguments["close"] = close;
    }

    await Surl("add_fault", arguments);
}

async Task<CallToolResult> Surl(string tool, Dictionary<string, object?> arguments)
{
    arguments["server_id"] = id;
    return await surl.CallToolAsync(tool, arguments);
}
