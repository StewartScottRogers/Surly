// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent builds each test URL from test data with curl-mcp's build_url, checks it with parse_url
// before using it, and fetches with the curl tool from a surl-mcp server, over the official MCP SDK.
// Run: dotnet run build-urls.cs -- <case>
//   build     build, check and use an HTTP and an FTP URL, then read the logins surl judged (the default)
//   raw       build the same URL without UrlEncode, and check it
//   in-clear  use the built URLs on a server that keeps its default, refusing passwords in clear
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "build";

// The test data: a user name, a password and a path, each with characters a URL reserves.
const string User = "ann@example.com", Password = "p@ss:w/rd 100%", FilePath = "/q1 report#final.txt";

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

if (which == "raw")
{
    // Without UrlEncode, build_url puts the parts in as they are, and parse_url cannot read the result back.
    JsonElement raw = await Call(curl, "build_url", new() { ["scheme"] = "ftp", ["user"] = User, ["password"] = Password, ["host"] = "127.0.0.1", ["port"] = "21", ["path"] = FilePath });
    Console.WriteLine($"built: {raw.GetProperty("url")}");
    JsonElement check = await Call(curl, "parse_url", new() { ["url"] = raw.GetProperty("url").GetString() });
    Console.WriteLine($"check: {check.GetProperty("failure").GetProperty("kind")}: {check.GetProperty("failure").GetProperty("text")}");
    Console.WriteLine("not used");
    return 1;
}

// The server: the account and the file the test data names, over HTTP and FTP. Both send the password
// in clear, which surl refuses unchecked unless --allow-plaintext-auth is given.
string[] arguments = which == "in-clear" ? ["--user", $"{User}:{Password}"] : ["--allow-plaintext-auth", "--user", $"{User}:{Password}"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "ftp://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { [FilePath[1..]] = "q1 final\n" },
});
JsonElement server = started.StructuredContent!.Value;
bool allPassed = true;
foreach (JsonElement bound in server.GetProperty("bound_urls").EnumerateArray())
{
    var listen = new Uri(bound.GetString()!);

    // Build: each part as it is in the test data; UrlEncode percent-encodes what the URL reserves.
    JsonElement built = await Call(curl, "build_url", new()
    {
        ["scheme"] = listen.Scheme, ["user"] = User, ["password"] = Password,
        ["host"] = listen.Host, ["port"] = listen.Port.ToString(), ["path"] = FilePath, ["flags"] = new[] { "UrlEncode" },
    });
    string url = built.GetProperty("url").GetString()!;
    Console.WriteLine($"{listen.Scheme} built: {url.Replace($":{listen.Port}/", ":<port>/")}");

    // Check: parse it back with UrlDecode; every part must be the test data again.
    JsonElement parts = (await Call(curl, "parse_url", new() { ["url"] = url, ["flags"] = new[] { "UrlDecode" } })).GetProperty("parts");
    bool same = parts.GetProperty("user").GetString() == User && parts.GetProperty("password").GetString() == Password
        && parts.GetProperty("path").GetString() == FilePath && parts.GetProperty("host").GetString() == listen.Host;
    Console.WriteLine($"{listen.Scheme} parts read back: {same}");

    // Use: fetch the file with the curl tool.
    JsonElement fetched = await Call(curl, "curl", new() { ["args"] = new[] { "-s", "-w", "%{response_code}", url } });
    string output = fetched.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n");
    Console.WriteLine($"{listen.Scheme} exit {fetched.GetProperty("exitCode")}: {output.Replace("\n", " ").Trim()}");
    allPassed &= fetched.GetProperty("exitCode").GetInt32() == 0;
}

// What surl recorded: the logins it judged, by account.
JsonElement events = (await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server.GetProperty("server_id").GetString(), ["kinds"] = new[] { "login_judged" },
})).StructuredContent!.Value;
foreach (JsonElement login in events.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"recorded: {login.GetProperty("scheme")} {login.GetProperty("account")} {(login.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}

return allPassed ? 0 : 1;

// One curl-mcp call; curl-mcp answers in text, as JSON.
static async Task<JsonElement> Call(McpClient host, string tool, Dictionary<string, object?> arguments)
{
    CallToolResult result = await host.CallToolAsync(tool, arguments);
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}
