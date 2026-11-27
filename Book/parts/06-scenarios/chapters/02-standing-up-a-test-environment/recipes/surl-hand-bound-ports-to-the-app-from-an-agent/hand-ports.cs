// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts HTTP and SMTP on free ports with surl-mcp, hands the bound URLs to the app
// (here, curl-mcp's curl tool standing in for it), and checks what the server recorded.
// Run: dotnet run hand-ports.cs -- <case>
//   hand         hand bound_urls to the app and read the mailbox afterwards (the default)
//   listen-urls  hand the app listen_urls, port 0 and all, instead
//   list         find the bound URLs again with list_servers
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "hand";

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

// The test environment: a greeting over HTTP and one mailbox over SMTP, both on free ports.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "smtp://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:secret" },
    ["files"] = new Dictionary<string, string> { ["greeting.txt"] = "Welcome aboard.\n" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string[] urls = server.GetProperty(which == "listen-urls" ? "listen_urls" : "bound_urls")
    .EnumerateArray().Select(u => u.GetString()!).ToArray();

if (which == "list")
{
    // An agent that lost start_server's answer asks the host again.
    CallToolResult listed = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
    JsonElement found = listed.StructuredContent!.Value.GetProperty("servers").EnumerateArray()
        .Single(s => s.GetProperty("server_id").GetString() == id);
    Console.WriteLine($"start_server said: {string.Join(" ", urls)}");
    Console.WriteLine($"list_servers says: {string.Join(" ", found.GetProperty("bound_urls").EnumerateArray())}");
    return 0;
}

// The app, handed the URLs: it fetches the greeting over HTTP, then mails it over SMTP.
string httpUrl = urls[0].TrimEnd('/') + "/", smtpUrl = urls[1].TrimEnd('/') + "/";
Console.WriteLine($"the app gets {httpUrl} and {smtpUrl}");
JsonElement fetched = await Curl(["-fsS", httpUrl + "greeting.txt"]);
if (fetched.GetProperty("exitCode").GetInt32() != 0)
{
    Console.WriteLine($"fetch failed: curl exit {fetched.GetProperty("exitCode")}, {fetched.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
    return 1;
}

string greeting = fetched.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd();
JsonElement mailed = await Curl(["-sS", "-u", "app@example.com:secret", "--mail-from", "app@example.com",
    "--mail-rcpt", "app@example.com", "-T", "-", smtpUrl + "example.com"], $"Subject: Welcome\r\n\r\n{greeting}\r\n");
Console.WriteLine($"the app mailed: {greeting} (curl exit {mailed.GetProperty("exitCode")})");

// What the server recorded, read from its mail store without a client.
CallToolResult messages = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "app@example.com", ["mailbox"] = "INBOX",
});
foreach (JsonElement message in messages.StructuredContent!.Value.GetProperty("messages").EnumerateArray())
{
    Console.WriteLine($"stored: UID {message.GetProperty("uid")}, {message.GetProperty("size")} bytes");
}

return 0;

// curl-mcp's curl tool runs curl's own command line; it answers in text, as JSON.
async Task<JsonElement> Curl(string[] curlArgs, string? stdin = null)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs, ["stdin"] = stdin });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
