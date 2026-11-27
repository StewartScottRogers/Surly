// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent finds out which login methods a server lets a client use: it starts the server through
// surl-mcp, reads what a request with no login is offered, tries Basic, Digest and Bearer in turn
// through curl-mcp, and reads back the logins the server judged.
// Run: dotnet run try-logins.cs [-- <server> [<password>]]
//   (no argument)  http://, surl's defaults, and app's right password
//   plaintext      http:// with --allow-plaintext-auth --auth digest,bearer
//   basic-only     https:// with --auth basic
//   <password>     log in as app with this password instead
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string server = args.Length > 0 ? args[0] : "";
string password = args.Length > 1 ? args[1] : "s3cret";

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

// The server: one password account, app, and one Bearer token (the empty user name), plus the
// options this run is about.
var arguments = new List<string> { "--user", "app:s3cret", "--user", ":tok-7f3a9c" };
string scheme = "http";
switch (server)
{
    case "plaintext":
        arguments.AddRange(["--allow-plaintext-auth", "--auth", "digest,bearer"]);
        break;
    case "basic-only":
        arguments.AddRange(["--cert", "server.pem", "--key", "server.key", "--auth", "basic"]);
        scheme = "https";
        break;
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { $"{scheme}://127.0.0.1:0" },
    ["arguments"] = arguments.ToArray(),
    ["files"] = new Dictionary<string, string> { ["report.txt"] = "quarterly report\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "report.txt";
long last = 0;

// What a request with no login is offered: the schemes of its WWW-Authenticate headers.
string head = await Curl("-s", "-D", "-", "-o", "body.out", url);
string[] offered = head.Split('\n').Where(l => l.StartsWith("WWW-Authenticate:", StringComparison.OrdinalIgnoreCase))
    .Select(l => l.Split(' ', 3)[1]).Distinct().ToArray();
Console.WriteLine($"no login: {head.Split('\n')[0].Split(' ')[1]}, offered [{string.Join(", ", offered)}]");

// Each method in turn, one curl call each: the status it got.
var methods = new (string Name, string[] Login)[]
{
    ("basic", ["--basic", "-u", $"app:{password}"]),
    ("digest", ["--digest", "-u", $"app:{password}"]),
    ("bearer", ["--oauth2-bearer", "tok-7f3a9c"]),
};
var usable = new List<string>();
foreach (var (name, login) in methods)
{
    string status = await Curl([.. login, "-s", "-o", "body.out", "-w", "%{http_code}", url]);
    Console.WriteLine($"{name}: {status}");
    if (status == "200")
    {
        usable.Add(name);
    }
}

Console.WriteLine($"usable: {string.Join(", ", usable)}");

// What the server judged, read once every exchange has ended: logins checked, refused or accepted.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement judged in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"recorded: {judged.GetProperty("mechanism")} {judged.GetProperty("account")} {(judged.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}

return 0;

// curl-mcp's curl tool, trusting server.pem: its standard output. Then wait, at most 10 seconds,
// for the server to end the exchange, so the record is complete before anyone reads it.
async Task<string> Curl(params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "--cacert", "server.pem", "--max-time", "10" }.Concat(more).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement output = answer.RootElement.GetProperty("standardOutput");

    CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["after_sequence"] = last, ["timeout_ms"] = 10000,
    });
    if (ended.StructuredContent!.Value.TryGetProperty("event", out JsonElement e))
    {
        last = e.GetProperty("sequence").GetInt64();
    }

    return output.ValueKind == JsonValueKind.Object ? output.GetProperty("content").GetString() ?? "" : "";
}
