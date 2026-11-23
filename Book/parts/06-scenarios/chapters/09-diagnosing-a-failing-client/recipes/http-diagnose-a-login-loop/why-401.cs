// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Clients that keep getting 401 run through curl-mcp's curl tool against a surl-mcp server with two accounts. For each
// one the agent reads the logins surl judged (get_events) and, where none was judged, the Authorization the client sent
// and the methods the server offered (get_exchanges), and says whether the method, the user or the password is wrong.
// Run: dotnet run why-401.cs
#:package ModelContextProtocol.Core@*

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

// The stand-in: HTTP with two accounts, the agent's own list of who may log in.
string[] accounts = ["app", "reports"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "app:s3cret", "--user", "reports:r3ports" },
    ["files"] = new Dictionary<string, string> { ["report.csv"] = "id,total\n1,42\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
long lastSequence = 0, lastExchange = 0;

// Each client fails as it is configured, retrying as an app in a loop would: -f with --retry-all-errors retries a 401.
string[] loop = ["-s", "-S", "-m", "10", "-f", "--retry", "2", "--retry-all-errors", "--retry-delay", "1"];
await Diagnose("wrong password", [.. loop, "--digest", "-u", "app:S3cret"]);
await Diagnose("wrong user", [.. loop, "--digest", "-u", "ap:s3cret"]);
await Diagnose("wrong method", [.. loop, "--ntlm", "-u", "app:s3cret"]);
await Diagnose("no login", [.. loop]);
await Diagnose("fixed", [.. loop, "--digest", "-u", "app:s3cret"]);
return 0;

async Task Diagnose(string client, string[] options)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = (string[])[.. options, "-o", "report.csv", url + "report.csv"] });
    JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{client}: curl exit {answer.GetProperty("exitCode")}");

    // The record is whole once every exchange that started has ended: poll for that, at most 10 s.
    List<JsonElement> events = [];
    for (int tries = 0; tries < 50; tries++)
    {
        events = await Events(new() { ["server_id"] = server, ["after_sequence"] = lastSequence });
        int opened = events.Count(e => e.GetProperty("kind").GetString() == "exchange_started");
        if (opened > 0 && opened == events.Count(e => e.GetProperty("kind").GetString() == "exchange_ended")) break;
        await Task.Delay(200);
    }

    lastSequence = events.Max(e => e.GetProperty("sequence").GetInt64());
    var judged = events.Where(e => e.GetProperty("kind").GetString() == "login_judged").ToList();
    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_exchange_id"] = lastExchange,
    });
    var exchanges = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().ToList();
    lastExchange = exchanges[^1].GetProperty("exchange_id").GetInt64();
    var statuses = exchanges.Select(x => Lines(x, "sent").Last(l => l.StartsWith("HTTP/1.1 "))[9..12]).ToList();
    Console.WriteLine($"  {exchanges.Count} connection(s), each ending {string.Join(", ", statuses.Distinct())}");

    if (judged.Count > 0)
    {
        // A judged login names the account the client claimed and how it logged in.
        foreach (var group in judged.GroupBy(e => (Account: e.GetProperty("account").GetString()!, Mechanism: e.GetProperty("mechanism").GetString()!, Accepted: e.GetProperty("is_accepted").GetBoolean())))
        {
            var (account, mechanism, accepted) = group.Key;
            string verdict = accepted ? "logged in" : accounts.Contains(account) ? "the password is wrong" : "the user name is wrong";
            Console.WriteLine($"  judged: {account}, {mechanism}, {(accepted ? "accepted" : "refused")} x{group.Count()}; {account} {(accounts.Contains(account) ? "is" : "is not")} an account: {verdict}");
        }
    }
    else
    {
        // No login judged: compare what the client sent with what the server offered.
        var sentMethods = exchanges.SelectMany(x => Lines(x, "received")).Where(l => l.StartsWith("Authorization: ")).Select(l => l.Split(' ')[1]).Distinct().ToList();
        var offered = exchanges.SelectMany(x => Lines(x, "sent")).Where(l => l.StartsWith("WWW-Authenticate: ")).Select(l => l.Split(' ')[1]).Distinct().ToList();
        Console.WriteLine($"  judged: none; client sent {(sentMethods.Count == 0 ? "no Authorization" : "Authorization " + string.Join(", ", sentMethods))}; server offered {string.Join(", ", offered)}: " +
            (sentMethods.Count == 0 ? "the client sends no login" : "the method is wrong"));
    }
}

async Task<List<JsonElement>> Events(Dictionary<string, object?> query)
{
    CallToolResult r = await surl.CallToolAsync("get_events", query);
    return r.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.Clone()).ToList();
}

static IEnumerable<string> Lines(JsonElement exchange, string direction) => exchange.GetProperty("chunks").EnumerateArray()
    .Where(c => c.GetProperty("direction").GetString() == direction)
    .SelectMany(c => c.GetProperty("data").GetString()!.Split("\r\n"));
