// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks how a mail client logs in over plain SMTP: surl-mcp runs the server, curl-mcp's
// curl tool is the client, and surl-mcp's events and exchanges show what the server judged and saw.
// Run: dotnet run log-in.cs -- <case>
//   cram   curl's own choice, CRAM-MD5, with the right password (the default)
//   wrong  CRAM-MD5 with a wrong password
//   plain  the client insists on PLAIN (--login-options AUTH=PLAIN)
//   raw    a client that writes AUTH PLAIN and AUTH LOGIN whatever is offered (plain.smtp over telnet)
//   loose  AUTH=PLAIN against a server started with --allow-plaintext-auth
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "cram";

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

// SMTP for example.com with two mailboxes; secure by default unless the case loosens it.
var arguments = new List<string> { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass" };
if (which == "loose")
{
    arguments.Insert(0, "--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" }, ["arguments"] = arguments.ToArray(),
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

if (which == "raw")
{
    // curl's telnet:// writes the file to the port as it is and prints what comes back.
    JsonElement raw = await Curl("-T", "plain.smtp", smtp.Replace("smtp://", "telnet://"));
    foreach (string line in raw.GetProperty("standardOutput").GetProperty("content").GetString()!.Split("\r\n")
        .Where(l => l.StartsWith("250 ") || l.StartsWith("235") || l.StartsWith("538")))
    {
        Console.WriteLine(line);
    }

    return 0;
}

// The client's send: the login, the envelope and the message from a file in this folder.
var send = new List<string> { "--crlf", "-u", $"ann@example.com:{(which == "wrong" ? "wrong" : "annpass")}" };
if (which is "plain" or "loose")
{
    send.AddRange(["--login-options", "AUTH=PLAIN"]);
}

send.AddRange(["--mail-from", "ann@example.com", "--mail-rcpt", "bob@example.com", "-T", "hi.eml", smtp + "example.com"]);
JsonElement sent = await Curl([.. send]);
Console.WriteLine($"curl exit {sent.GetProperty("exitCode")}");

// What the server judged: one login_judged event per login it decided.
CallToolResult logins = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement login in logins.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"login_judged: {login.GetProperty("mechanism")} {login.GetProperty("account")} is_accepted {login.GetProperty("is_accepted").GetRawText()}");
}

// What the client put on the wire: the AUTH line, and whether the password is there as is or in base64.
await AllEndedAsync();
CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
string received = string.Concat(exchanges.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
    .Where(c => c.GetProperty("direction").GetString() == "received").Select(c => c.GetProperty("data").GetString()));
string auth = received.Split("\r\n").FirstOrDefault(l => l.StartsWith("AUTH ")) ?? "(no AUTH)";
Console.WriteLine($"get_exchanges: {auth}; password on the wire: {received.Contains("annpass") || received.Contains("AGFubkBleGFtcGxlLmNvbQBhbm5wYXNz")}");
return sent.GetProperty("exitCode").GetInt32() == 0 ? 0 : 1;

// The server records an exchange's last bytes and its end a moment after curl has its answer: ask
// until every exchange that started has ended, for at most 10 s.
async Task AllEndedAsync()
{
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
    {
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "exchange_started", "exchange_ended" } });
        var kinds = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("kind").GetString()).ToList();
        if (kinds.Count(k => k == "exchange_started") == kinds.Count(k => k == "exchange_ended")) return;
    }
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
