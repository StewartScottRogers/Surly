// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent finds where an app's mail went: surl-mcp runs the mail server, curl-mcp's curl tool plays
// the app, and when nothing reaches Ann the agent reads the SMTP dialogue from get_exchanges and says
// who the envelope named.
// Run: dotnet run read-dialogue.cs -- <case>
//   astray   the app names the test address left in its settings (the default)
//   landed   the same, then what each mailbox holds
//   login    the same, then the client's lines before MAIL FROM
//   stored   the same, then the top of the copy the server stored
//   fixed    the app names Ann
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "astray";
string recipient = which == "fixed" ? "ann@example.com" : "test@example.com";

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

// SMTP for example.com: the app's mailbox, Ann's, and the test address from the app's development.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass", "--mailbox", "test@example.com:testpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The app's send: its envelope names the address in its settings.
CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-S", "--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", recipient, "-T", "order.eml", "-w", "%{response_code}", smtp + "shop.example.com" },
});
using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
Console.WriteLine($"curl exit {answer.RootElement.GetProperty("exitCode").GetInt32()}, reply {answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()}");

// The exchange is complete once the server has ended it; wait for that, then for Ann's mail, each with a bound.
await Wait(new() { ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp", ["timeout_ms"] = 10000 });
if (!await Wait(new() { ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = "ann@example.com", ["timeout_ms"] = 2000 }))
{
    Console.WriteLine("nothing for ann@example.com within 2 s");
    List<(string Who, string Line)> dialogue = await Dialogue();
    switch (which)
    {
        case "astray":
            // The agent's reading: who the envelope named, and whom the headers name.
            int mailFrom = dialogue.FindIndex(l => l.Who == "C" && l.Line.StartsWith("MAIL FROM"));
            dialogue.Skip(mailFrom).ToList().ForEach(l => Console.WriteLine($"{l.Who}: {l.Line}"));
            var accepted = dialogue.Zip(dialogue.Skip(1))
                .Where(p => p.First.Line.StartsWith("RCPT TO:<") && p.Second.Who == "S" && p.Second.Line.StartsWith("250"))
                .Select(p => p.First.Line["RCPT TO:<".Length..^1]);
            string to = dialogue.First(l => l.Who == "C" && l.Line.StartsWith("To: ")).Line["To: ".Length..];
            Console.WriteLine($"the envelope delivered to {string.Join(", ", accepted)}; the To header says {to}");
            break;
        case "landed":
            foreach (string owner in new[] { "test@example.com", "ann@example.com" })
            {
                CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
                {
                    ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX",
                });
                Console.WriteLine($"{owner} holds {listed.StructuredContent!.Value.GetProperty("messages").GetArrayLength()}");
            }

            break;
        case "login":
            Console.WriteLine("before MAIL FROM:");
            dialogue.TakeWhile(l => !l.Line.StartsWith("MAIL FROM")).Where(l => l.Who == "C").ToList().ForEach(l => Console.WriteLine($"C: {l.Line}"));
            break;
        case "stored":
            CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
            {
                ["server_id"] = id, ["owner"] = "test@example.com", ["mailbox"] = "INBOX", ["uid"] = 1,
            });
            Console.WriteLine("stored:");
            read.StructuredContent!.Value.GetProperty("text").GetString()!.Split("\r\n").Take(3).ToList().ForEach(Console.WriteLine);
            break;
    }

    return 1;
}

Console.WriteLine("ann@example.com has the notice");
(await Dialogue()).Where(l => l.Line.StartsWith("RCPT TO")).ToList().ForEach(l => Console.WriteLine($"{l.Who}: {l.Line}"));
return 0;

// wait_for_event answers the event, or {"timed_out":true} when the bound passes.
async Task<bool> Wait(Dictionary<string, object?> query)
{
    query["server_id"] = id;
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", query);
    return !waited.StructuredContent!.Value.TryGetProperty("timed_out", out _);
}

// The last SMTP exchange from get_exchanges, a line per protocol line: C what the client sent (the
// server received), S what the server sent.
async Task<List<(string Who, string Line)>> Dialogue()
{
    CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
    JsonElement exchange = got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last();
    var lines = new List<(string, string)>();
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        string who = chunk.GetProperty("direction").GetString() == "received" ? "C" : "S";
        string data = chunk.GetProperty("data").GetString()!;
        foreach (string line in (data.EndsWith("\r\n") ? data[..^2] : data).Split("\r\n"))
        {
            lines.Add((who, line));
        }
    }

    return lines;
}
