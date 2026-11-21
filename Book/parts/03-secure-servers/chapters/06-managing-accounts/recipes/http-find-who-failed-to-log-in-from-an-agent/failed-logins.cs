// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Several clients share one surl-mcp server over HTTPS; one keeps failing to log in. The agent
// reads the server's login events and names the account and method behind the failures.
// Run: dotnet run failed-logins.cs [-- summary|exchange]
//   summary   every judged login counted by account, method and verdict (the default)
//   exchange  the start event of the first refused login's exchange: where that client came from
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "summary";

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

// One HTTPS server, three accounts, one file.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "billing:b1-secret", "--user", "search:s1-secret", "--user", "reports:r1-secret" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

// The clients: billing with Digest, search with Basic, and a reports job with a stale password
// that tries three times, then one that types its own name wrong.
await Fetch("--digest", "billing:b1-secret");
await Fetch("--basic", "search:s1-secret");
for (int i = 0; i < 3; i++)
{
    await Fetch("--basic", "reports:r0-secret");
}

await Fetch("--digest", "biling:b1-secret");

// What the server judged.
CallToolResult found = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
});
var logins = found.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToList();

if (mode == "exchange")
{
    // The first refused login's exchange: its exchange_started event says which address it came from.
    JsonElement first = logins.First(l => !l.GetProperty("is_accepted").GetBoolean());
    long exchange = first.GetProperty("exchange_id").GetInt64();
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "exchange_started" },
    });
    JsonElement start = events.StructuredContent!.Value.GetProperty("events").EnumerateArray()
        .Single(e => e.GetProperty("exchange_id").GetInt64() == exchange);
    Console.WriteLine($"first refusal: exchange {exchange}, {first.GetProperty("account")}, from {start.GetProperty("client")}");
    return 0;
}

// Counted by account, method and verdict, failures first.
var counted = logins
    .GroupBy(l => (Account: l.GetProperty("account").GetString(), Mechanism: l.GetProperty("mechanism").GetString(),
        Accepted: l.GetProperty("is_accepted").GetBoolean(), ByRule: l.GetProperty("is_refused_by_fault").GetBoolean()))
    .OrderBy(g => g.Key.Accepted).ThenByDescending(g => g.Count()).ThenBy(g => g.Key.Account);
foreach (var group in counted)
{
    string verdict = group.Key.Accepted ? "accepted" : group.Key.ByRule ? "refused by a fault rule" : "refused";
    Console.WriteLine($"{group.Key.Account} {group.Key.Mechanism}: {group.Count()} {verdict}");
}

return 0;

// curl-mcp's curl tool: one GET as a client makes it, trusting server.pem.
async Task Fetch(string method, string login)
{
    await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-o", "hello.out", "--cacert", "server.pem", method, "-u", login, url },
    });
}
