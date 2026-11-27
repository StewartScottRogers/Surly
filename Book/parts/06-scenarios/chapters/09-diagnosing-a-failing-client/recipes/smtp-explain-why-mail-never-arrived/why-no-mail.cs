// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An app's mail goes missing three ways. The app sends through curl-mcp's curl tool to a surl-mcp mail server; for each
// send the agent waits for the exchange to end, reads the refusals in get_exchanges, then waits for a delivery event for
// each recipient the server accepted, and says what stopped the mail. Run: dotnet run why-no-mail.cs
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The app's two messages are here.
string here = Directory.GetCurrentDirectory();
File.WriteAllText("report.eml", "From: app@example.com\r\nTo: ann@example.com\r\nSubject: Nightly report\r\n\r\nAll 42 jobs finished.\r\n");
var big = new StringBuilder("From: app@example.com\r\nTo: ann@example.com\r\nSubject: Nightly report\r\nMIME-Version: 1.0\r\n" +
    "Content-Type: multipart/mixed; boundary=b1\r\n\r\n--b1\r\nContent-Type: text/plain\r\n\r\nAll 42 jobs finished.\r\n" +
    "--b1\r\nContent-Type: text/csv; name=jobs.csv\r\nContent-Transfer-Encoding: base64\r\n\r\n");
for (int line = 0; line < 40; line++) big.Append(Convert.ToBase64String(Encoding.ASCII.GetBytes($"job,{line:D3},ok,finished at 02:{line:D2}\n" + "padding,padding\n")) + "\r\n");
File.WriteAllText("report-with-log.eml", big.Append("--b1--\r\n").ToString());

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

// The mail server as it runs for the app: one domain, three mailboxes, and messages of at most 2 KiB.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass",
        "--mailbox", "bob@example.com:bobpass", "--max-filesize", "2k",
    },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
long lastSequence = 0;

await Explain("the report with its job log, to ann", "report-with-log.eml", ["ann@example.com"]);
await Explain("the report, to ann and bob", "report.eml", ["ann@example.com", "bob@example.org"], ["--mail-rcpt-allowfails"]);
await Explain("the report, to carol", "report.eml", ["carol@example.com"]);
return 0;

async Task Explain(string what, string file, string[] to, string[]? extra = null)
{
    // The app's send, quiet as an app runs it.
    var arguments = new List<string> { "-s", "-m", "10", "-u", "app@example.com:apppass", "--mail-from", "app@example.com" };
    foreach (string rcpt in to) arguments.AddRange(["--mail-rcpt", rcpt]);
    arguments.AddRange(extra ?? []);
    arguments.AddRange(["-T", file, smtp + "app.example.com"]);
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.ToArray() });
    JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{what}: curl exit {answer.GetProperty("exitCode")}, standardError \"{answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}\"");

    // Wait, at most 10 s, until surl has ended the exchange; then read it.
    long before = lastSequence;
    CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp", ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
    });
    JsonElement end = ended.StructuredContent!.Value.GetProperty("event");
    lastSequence = end.GetProperty("sequence").GetInt64();
    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_exchange_id"] = end.GetProperty("exchange_id").GetInt64() - 1, ["max_exchanges"] = 1,
    });

    // Each reply, with the line the client sent just before it.
    var pairs = new List<(string Sent, string Reply)>();
    string lastReceived = "";
    foreach (JsonElement chunk in recorded.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray())
    {
        foreach (string line in chunk.GetProperty("data").GetString()!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (chunk.GetProperty("direction").GetString() == "received") lastReceived = line;
            else pairs.Add((lastReceived, line));
        }
    }

    var refused = pairs.Where(p => p.Reply[0] is '4' or '5').ToList();
    Console.WriteLine($"  refused: {(refused.Count == 0 ? "nothing" : string.Join("; ", refused.Select(p => $"{p.Sent} -> {p.Reply}")))}");
    string? limit = pairs.Select(p => p.Reply).FirstOrDefault(r => r.StartsWith("250-SIZE ") || r.StartsWith("250 SIZE "));
    if (refused.Any(p => p.Reply.StartsWith("552"))) Console.WriteLine($"  the server's EHLO reply said: {limit?[4..]}; the message is {new FileInfo(file).Length} bytes");

    // Each recipient the server accepted with 250: was the mail delivered to it? Wait at most 5 s for each.
    var accepted = pairs.Where(p => p.Sent.StartsWith("RCPT TO:<") && p.Reply.StartsWith("250")).Select(p => p.Sent[9..^1]).ToList();
    bool took = pairs.Any(p => p.Sent == "." && p.Reply.StartsWith("250"));
    foreach (string rcpt in took ? accepted : [])
    {
        CallToolResult delivered = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = rcpt, ["after_sequence"] = before, ["timeout_ms"] = 5000,
        });
        if (delivered.StructuredContent!.Value.TryGetProperty("event", out JsonElement e))
        {
            Console.WriteLine($"  {rcpt}: accepted, delivered to {e.GetProperty("name").GetString()}");
            lastSequence = Math.Max(lastSequence, e.GetProperty("sequence").GetInt64());
        }
        else
        {
            CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = server, ["owner"] = rcpt });
            Console.WriteLine($"  {rcpt}: accepted with 250, delivered nowhere within 5 s; list_mailboxes: {boxes.Content.OfType<TextContentBlock>().First().Text.Trim()}");
        }
    }

    if (!took) Console.WriteLine("  the message itself was never accepted, so nothing was delivered");
}
