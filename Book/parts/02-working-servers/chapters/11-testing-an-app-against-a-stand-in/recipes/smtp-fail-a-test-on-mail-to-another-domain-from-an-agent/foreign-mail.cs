// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks that an app cannot mail outside its domains: surl-mcp serves example.com and
// example.org, the app's sends go through curl-mcp's curl tool, and get_exchanges shows each refusal.
// Run: dotnet run foreign-mail.cs
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// SMTP on a free port, serving two domains.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com", "--mail-domain", "example.org",
        "--mailbox", "billing@example.com:billpass", "--mailbox", "ann@example.com:annpass", "--mailbox", "sales@example.org:salespass",
    },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
long lastSequence = 0;

// The app's send to an outside address; then what the server answered its RCPT TO, from the record.
Console.WriteLine($"carol@elsewhere.org: {await Send("billing@example.com", "carol@elsewhere.org", "invoice.eml")}");
List<(string Rcpt, string Reply)> rcpts = await Rcpts();
Console.WriteLine($"recorded: {rcpts[^1].Rcpt} -> {rcpts[^1].Reply}");

Console.WriteLine($"sales@example.org: {await Send("billing@example.com", "sales@example.org", "invoice.eml")}");
Console.WriteLine($"ann@mail.example.com: {await Send("billing@example.com", "ann@mail.example.com", "invoice.eml")}");
Console.WriteLine($"ann@example.com.evil.org: {await Send("billing@example.com", "ann@example.com.evil.org", "invoice.eml")}");

// Letter case: accepted, and the delivery into ann's mailbox comes within ten seconds.
Console.WriteLine($"ANN@EXAMPLE.COM: {await Send("billing@example.com", "ANN@EXAMPLE.COM", "invoice.eml")}");
CallToolResult delivered = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = "ann@example.com", ["timeout_ms"] = 10000,
});
Console.WriteLine($"delivered: {delivered.StructuredContent!.Value.GetProperty("event").GetProperty("name").GetString()}");

// Only the envelope's recipients are judged: not the sender, not the header fields.
Console.WriteLine($"from noreply@elsewhere.org: {await Send("noreply@elsewhere.org", "ann@example.com", "invoice.eml")}");
Console.WriteLine($"Cc carol@elsewhere.org, sent to ann: {await Send("billing@example.com", "ann@example.com", "cc-outside.eml")}");

// Every recipient the server refused, from the whole record.
Console.WriteLine($"refused: {string.Join(", ", (await Rcpts()).Where(r => r.Reply.StartsWith("550 ")).Select(r => r.Rcpt["RCPT TO:".Length..]))}");
return 0;

// The app's send through the curl tool; then a bounded wait until surl has ended the exchange.
async Task<string> Send(string sender, string recipient, string file)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[]
        {
            "-s", "-S", "-w", "%{response_code}", "-u", "billing@example.com:billpass",
            "--mail-from", sender, "--mail-rcpt", recipient, "-T", file, smtp + "app.example.com",
        },
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp",
        ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
    });
    lastSequence = waited.StructuredContent!.Value.GetProperty("event").GetProperty("sequence").GetInt64();
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    return $"curl exit {answer.GetProperty("exitCode")}, {answer.GetProperty("standardOutput").GetProperty("content").GetString()}"
        + (error.Length > 0 ? ", " + error : "");
}

// Each RCPT TO line any client sent, with the server's next line, across every SMTP exchange recorded.
async Task<List<(string Rcpt, string Reply)>> Rcpts()
{
    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["scheme"] = "smtp", ["max_exchanges"] = 100,
    });
    var found = new List<(string, string)>();
    foreach (JsonElement exchange in recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
    {
        string[] lines = exchange.GetProperty("chunks").EnumerateArray()
            .SelectMany(c => c.GetProperty("data").GetString()!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)).ToArray();
        for (int i = 0; i + 1 < lines.Length; i++)
        {
            if (lines[i].StartsWith("RCPT TO:"))
            {
                found.Add((lines[i], lines[i + 1]));
            }
        }
    }

    return found;
}
