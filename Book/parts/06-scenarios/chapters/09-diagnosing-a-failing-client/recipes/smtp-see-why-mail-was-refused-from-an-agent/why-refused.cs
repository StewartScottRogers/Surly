// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent finds why an app's mail was refused: the app sends through curl-mcp's curl tool and fails;
// the agent waits for the SMTP exchange to end and reads the refused command and reply from
// surl-mcp's get_exchanges, the mailbox from list_mailboxes, and a refused login from get_events.
// Run: dotnet run why-refused.cs
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

// SMTP on a free port; ops@example.com holds at most 200 bytes.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass",
        "--mailbox", "ops@example.com:opspass;quota=200", "--mailbox", "app@example.com:apppass",
    },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
long lastSequence = 0;

// The failure as the app's owner meets it: a quiet send that fails, and an empty mailbox.
JsonElement quiet = await App(["-s"], "ann@example.org");
Console.WriteLine($"app, to ann@example.org: curl exit {quiet.GetProperty("exitCode")}, standardError \"{Error(quiet)}\"");
Console.WriteLine($"ann's INBOX holds {await Count("ann@example.com")}");
Console.WriteLine($"refused: {await WhyRefused()}");
JsonElement loud = await App(["-s", "-S"], "ann@example.org");
Console.WriteLine($"with -S: curl exit {loud.GetProperty("exitCode")}, {Error(loud)}");
await WhyRefused();

// The fix, and the report arriving: delivery is asynchronous, so wait for its event first.
JsonElement fixedSend = await App(["-s", "-S", "-w", "%{response_code}"], "ann@example.com");
Console.WriteLine($"app, to ann@example.com: {Out(fixedSend)}; {await WhyRefused()}");
CallToolResult delivered = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail",
    ["name"] = "ann@example.com/INBOX/;UID=1", ["timeout_ms"] = 10000,
});
Console.WriteLine($"delivered event: {delivered.StructuredContent!.Value.TryGetProperty("event", out _)}; ann's INBOX holds {await Count("ann@example.com")}");

// The other ways a send fails.
await App(["-s"], "ann@example.com", login: null);
Console.WriteLine($"no login: {await WhyRefused()}");
await App(["-s"], "ann@example.com", login: "app@example.com:wrongpass");
Console.WriteLine($"wrong password: {await WhyRefused()}");
CallToolResult judged = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" }, ["account"] = "app@example.com",
});
JsonElement refusedLogin = judged.StructuredContent!.Value.GetProperty("events").EnumerateArray().Last();
Console.WriteLine($"login_judged: {refusedLogin.GetProperty("account").GetString()} {refusedLogin.GetProperty("mechanism").GetString()} is_accepted {refusedLogin.GetProperty("is_accepted")}");
JsonElement full = await App(["-s", "-S"], "ops@example.com");
Console.WriteLine($"to a full mailbox: {await WhyRefused()}");
Console.WriteLine($"curl said: exit {full.GetProperty("exitCode")}, {Error(full)}");
return 0;

// The app: curl's send through the curl tool, quiet as an app runs it.
async Task<JsonElement> App(string[] quietness, string to, string? login = "app@example.com:apppass")
{
    var arguments = new List<string>(quietness);
    if (login is not null)
    {
        arguments.AddRange(["-u", login]);
    }

    arguments.AddRange(["--mail-from", "app@example.com", "--mail-rcpt", to, "-T", "report.eml", smtp + "app.example.com"]);
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// The diagnosis: wait (at most 10 s) until surl has ended the exchange, then each reply of 4xx or 5xx
// in the last SMTP exchange, with the line the client sent just before it.
async Task<string> WhyRefused()
{
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "smtp",
        ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
    });
    lastSequence = waited.StructuredContent!.Value.GetProperty("event").GetProperty("sequence").GetInt64();
    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["scheme"] = "smtp", ["max_exchanges"] = 100,
    });
    JsonElement exchange = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last();
    var found = new List<string>();
    string lastReceived = "";
    foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
    {
        foreach (string line in chunk.GetProperty("data").GetString()!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (chunk.GetProperty("direction").GetString() == "received")
            {
                lastReceived = line;
            }
            else if (line[0] is '4' or '5')
            {
                found.Add($"{lastReceived} -> {line}");
            }
        }
    }

    return found.Count == 0 ? "no refusal" : string.Join("; ", found);
}

async Task<int> Count(string owner)
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = owner });
    return boxes.StructuredContent!.Value.GetProperty("mailboxes").EnumerateArray()
        .Single(m => m.GetProperty("name").GetString() == "INBOX").GetProperty("message_count").GetInt32();
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

string Error(JsonElement answer) => answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
