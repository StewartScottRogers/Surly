// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent loses an SMTP server's final reply on purpose: surl-mcp runs the mail server and cuts the
// next connection one byte into the reply after the message; curl-mcp's curl tool is the sender, with
// a retry; the agent then counts the copies of the order's Message-ID and the deliveries behind them.
// Run: dotnet run lost-reply.cs [-- <case>]
//   (no argument)  the sender with one retry
//   tries          the same, then what the server sent from its 354 on in each exchange
//   no-retry       the sender without a retry
//   cut-before     the cut one byte sooner, at the end of the 354
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "retry";

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

// SMTP for the shop to send, IMAP to search Ann's mailbox, one mail store.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "shop@example.com:shoppass", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

// The fault: cut the next SMTP connection cleanly once the server has sent 320 bytes, one past its 354.
int cut = which == "cut-before" ? 319 : 320;
CallToolResult added = await Surl("add_fault", new() { ["fault"] = $"drop;scheme=smtp;after-bytes={cut};times=1", ["close"] = "graceful" });
Console.WriteLine($"add_fault: {added.StructuredContent!.Value.GetProperty("fault_id").GetString()}");

// The sender: curl with one retry on any error, or none.
string[] retry = which == "no-retry" ? [] : ["--retry", "1", "--retry-all-errors", "--retry-delay", "1"];
JsonElement sent = await Curl(["-s", "-S", "--crlf", "-m", "10", "-u", "shop@example.com:shoppass", "--mail-from", "shop@example.com", "--mail-rcpt", "ann@example.com",
    .. retry, "-T", "order.eml", "-w", "%{response_code} %{num_retries}", smtp + "shop.example.com"]);
string[] written = sent.GetProperty("standardOutput").GetProperty("content").GetString()!.Split(' ');
Console.WriteLine($"curl exit {sent.GetProperty("exitCode").GetInt32()}, reply {written[0]}, retries {written[1]}");

// The count: the UIDs whose Message-ID header is the order's, by an IMAP search through the curl tool.
JsonElement found = await Curl(["-s", "-S", "-u", "ann@example.com:annpass", "-X", "UID SEARCH HEADER Message-ID <order-1042@shop.example.com>", imap + "INBOX"]);
string[] uids = found.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()["* SEARCH".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
Console.WriteLine($"<order-1042@shop.example.com>: UIDs {string.Join(" ", uids)}");

// The deliveries behind them, from the server's events, and the exchange each came from.
JsonElement delivered = (await Surl("get_events", new() { ["kinds"] = new[] { "store_changed" }, ["store"] = "mail" })).StructuredContent!.Value.GetProperty("events");
var exchanges = delivered.EnumerateArray().Where(e => e.GetProperty("change").GetString() == "delivered").Select(e => e.GetProperty("exchange_id").GetRawText()).ToList();
Console.WriteLine($"deliveries: {exchanges.Count}, from {exchanges.Distinct().Count()} exchange(s)");
Console.WriteLine(uids.Length == 1 ? "PASS" : $"FAIL: the order was stored {uids.Length} times");

if (which == "tries")
{
    // Both exchanges must have ended before their records are read; wait with a bound.
    for (int waited = 0; waited < 100; waited++)
    {
        JsonElement ended = (await Surl("get_events", new() { ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp" })).StructuredContent!.Value.GetProperty("events");
        if (ended.GetArrayLength() >= 2)
        {
            break;
        }

        await Task.Delay(100);
    }

    // For each SMTP exchange: the bytes the server had sent by the end of its 354, and what it sent after.
    foreach (JsonElement exchange in (await Surl("get_exchanges", new() { ["scheme"] = "smtp" })).StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
    {
        long total = 0, at354 = -1;
        var after = new List<string>();
        foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray().Where(c => c.GetProperty("direction").GetString() == "sent"))
        {
            string data = chunk.GetProperty("data").GetString()!;
            total += Encoding.UTF8.GetByteCount(data);
            if (at354 >= 0)
            {
                after.Add(data.TrimEnd('\r', '\n'));
            }
            else if (data.StartsWith("354 "))
            {
                at354 = total;
            }
        }

        Console.WriteLine($"exchange {exchange.GetProperty("exchange_id").GetRawText()}: {at354} bytes to the end of the 354, then {string.Join(" | ", after)}");
    }
}

return uids.Length == 1 ? 0 : 1;

// The curl tool: curl's own arguments; it answers exitCode, standardOutput and standardError as JSON.
async Task<JsonElement> Curl(string[] curlArgs)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    return JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

async Task<CallToolResult> Surl(string tool, Dictionary<string, object?> arguments)
{
    arguments["server_id"] = id;
    return await surl.CallToolAsync(tool, arguments);
}
