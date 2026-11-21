// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves two services' files from one surl-mcp server, one account each, lets each
// fetch with curl-mcp, and reads back from the server's events and exchanges who fetched what.
// Run: dotnet run who-fetched.cs [-- logins|by-account|nobody]
//   logins      every login the server judged, then what each account fetched (the default)
//   by-account  only the events get_events keeps for the account billing
//   nobody      get_events for an account no request named
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "logins";

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

// One server, one account per service, two files in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "billing:b1-secret", "--user", "search:s1-secret" },
    ["files"] = new Dictionary<string, string> { ["invoices.csv"] = "id,total\n1,40\n", ["index.html"] = "<p>search index</p>\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// Each service fetches as itself; each curl call is its own process, so its own connection.
Console.WriteLine($"billing GET /invoices.csv: {await Fetch("billing:b1-secret", "invoices.csv")}");
Console.WriteLine($"search GET /index.html: {await Fetch("search:s1-secret", "index.html")}");
Console.WriteLine($"search, wrong password, GET /invoices.csv: {await Fetch("search:guess", "invoices.csv")}");

switch (which)
{
    case "by-account" or "nobody":
        // Every kept event that names the account or belongs to an exchange that judged a login for it.
        string account = which == "nobody" ? "nobody" : "billing";
        foreach (JsonElement e in (await Events(new() { ["account"] = account })).EnumerateArray())
        {
            Console.WriteLine($"{account}: exchange {e.GetProperty("exchange_id")} {e.GetProperty("kind")}");
        }

        Console.WriteLine($"{account}: end of events");
        break;

    default:
        // Every login the server judged, then the request lines of each accepted login's exchange.
        JsonElement logins = await Events(new() { ["kinds"] = new[] { "login_judged" } });
        foreach (JsonElement login in logins.EnumerateArray())
        {
            string verdict = login.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused";
            Console.WriteLine($"exchange {login.GetProperty("exchange_id")}: {login.GetProperty("mechanism")} {login.GetProperty("account")} {verdict}");
        }

        foreach (JsonElement login in logins.EnumerateArray().Where(l => l.GetProperty("is_accepted").GetBoolean()))
        {
            long exchange = login.GetProperty("exchange_id").GetInt64();
            CallToolResult found = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
            {
                ["server_id"] = id, ["after_exchange_id"] = exchange - 1, ["max_exchanges"] = 1,
            });
            var paths = found.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
                .Where(c => c.GetProperty("direction").GetString() == "received")
                .SelectMany(c => c.GetProperty("data").GetString()!.Split("\r\n"))
                .Where(line => line.StartsWith("GET "))
                .Select(line => line.Split(' ')[1])
                .Distinct();
            Console.WriteLine($"{login.GetProperty("account")} fetched: {string.Join(", ", paths)}");
        }

        break;
}

return 0;

// curl-mcp's curl tool: a Digest login; -w prints the status code after the body.
async Task<string> Fetch(string login, string path)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "--digest", "-u", login, "-w", "%{http_code}", url + path },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
    return output[^3..];
}

// surl-mcp's get_events for this server, with the given filters: the events list.
async Task<JsonElement> Events(Dictionary<string, object?> filters)
{
    filters["server_id"] = id;
    CallToolResult result = await surl.CallToolAsync("get_events", filters);
    return result.StructuredContent!.Value.GetProperty("events").Clone();
}
