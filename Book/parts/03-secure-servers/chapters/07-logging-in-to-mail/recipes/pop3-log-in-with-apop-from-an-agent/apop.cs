// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks a POP3 login with APOP: surl-mcp runs the server, curl-mcp's curl tool is the client, and
// surl-mcp's events and exchanges show what the server judged and what crossed the wire.
// Run: dotnet run apop.cs [-- <case>]
//   apop     a server started with --auth apop (the default argument)
//   default  the default server: no timestamp, APOP refused, curl logs in with CRAM-MD5
//   both     --auth apop,cram-md5
#:package ModelContextProtocol.Core@2.4.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "apop";

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

// POP3 for example.com with one mailbox. APOP is not in the default methods: --auth names it.
var arguments = new List<string> { "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass" };
if (which == "apop")
{
    arguments.AddRange(["--auth", "apop"]);
}

if (which == "both")
{
    arguments.AddRange(["--auth", "apop,cram-md5"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "pop3://127.0.0.1:0" }, ["arguments"] = arguments.ToArray(),
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string pop3 = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Raw POP3: curl's telnet:// writes a fixture to the port as it is and prints every reply, as lines.
async Task<string[]> Talk(string file)
{
    JsonElement result = await Curl("-T", file, pop3.Replace("pop3://", "telnet://"));
    return result.GetProperty("standardOutput").GetProperty("content").GetString()!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}

string[] raw = await Talk("apop.pop");
Console.WriteLine($"greeting: {(Regex.IsMatch(raw[0], @"^\+OK surl ready <[0-9a-f]+\.\d+@surl>$") ? "ends in a timestamp" : raw[0])}");
Console.WriteLine($"raw APOP: {raw[1]}");

JsonElement login = await Curl("-u", "ann@example.com:annpass", pop3);
Console.WriteLine($"curl login: exit {login.GetProperty("exitCode")}");

// What the server judged: one login_judged event per decision.
CallToolResult judged = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "login_judged" } });
foreach (JsonElement e in judged.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"login_judged: {e.GetProperty("mechanism").GetString()} {e.GetProperty("account").GetString()} is_accepted {e.GetProperty("is_accepted").GetRawText()}");
}

if (which == "apop")
{
    // The exchange of that login: the timestamp is in what the server sent, the APOP line in what it received.
    await AllEndedAsync();
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "pop3" });
    JsonElement chunks = exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .Select(x => x.GetProperty("chunks")).Last(c => c.EnumerateArray().Any(k => k.GetProperty("data").GetString()!.Contains("APOP ann@")));
    string Wire(string direction) => string.Concat(chunks.EnumerateArray().Where(c => c.GetProperty("direction").GetString() == direction).Select(c => c.GetProperty("data").GetString()));
    string stamp = Regex.Match(Wire("sent"), "<[^>]+>").Value;
    string digest = Regex.Match(Wire("received"), @"APOP ann@example\.com ([0-9a-f]{32})").Groups[1].Value;
    Console.WriteLine($"digest is MD5 of timestamp and password: {digest == Convert.ToHexStringLower(MD5.HashData(Encoding.ASCII.GetBytes(stamp + "annpass")))}");
    Console.WriteLine($"password on the wire: {Wire("received").Contains("annpass")}");
    Console.WriteLine($"wrong password: curl exit {(await Curl("-u", "ann@example.com:wrong", pop3)).GetProperty("exitCode")}");
    Console.WriteLine($"USER: {(await Talk("userpass.pop"))[1]}");
    Console.WriteLine($"CAPA lists SASL: {(await Talk("capa.pop")).Any(l => l.StartsWith("SASL"))}");
}

return 0;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

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
