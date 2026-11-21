// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts an MQTT broker with one account using surl-mcp, logs in and publishes with
// curl-mcp's curl tool, and asks the broker what it kept.
// Run: dotnet run log-in.cs -- <scheme> <password>
//   mqtts s3cret   TLS and the right password (the default)
//   mqtts wrong    TLS and a wrong password
//   mqtts none     TLS and no user name
//   mqtt s3cret    no TLS, the right password
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scheme = args.Length > 0 ? args[0] : "mqtts";
string password = args.Length > 1 ? args[1] : "s3cret";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. It holds
// server.pem and server.key, which openssl wrote.
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

// surl-mcp: one account and no loosening option; --topic keeps what is published under home.
var serverArguments = new List<string> { "--user", "alice:s3cret", "--topic", "home/#;retain" };
if (scheme == "mqtts") serverArguments.AddRange(["--cert", "server.pem", "--key", "server.key"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { $"{scheme}://127.0.0.1:0" },
    ["arguments"] = serverArguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp's curl tool takes curl's arguments, one list item each: -u is the login, --cacert the
// certificate to trust, -d the message. -sS hides the progress meter and keeps the error text.
var curlArgs = new List<string> { "-sS", "--cacert", "server.pem" };
if (password != "none") curlArgs.AddRange(["-u", $"alice:{password}"]);
curlArgs.AddRange(["-d", "21.5", url + "home/temp"]);
CallToolResult published = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
JsonElement answer = JsonDocument.Parse(published.Content.OfType<TextContentBlock>().Single().Text).RootElement;
JsonElement error = answer.GetProperty("standardError");
string said = error.ValueKind == JsonValueKind.Null ? "" : error.GetProperty("content").GetString()!.Trim();
Console.WriteLine($"curl exit {answer.GetProperty("exitCode").GetInt32()} {said}".TrimEnd());

// surl-mcp: the message the broker kept for the topic, if the login let the publish through.
CallToolResult kept = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
{
    ["server_id"] = server, ["topic"] = "home/temp",
});
Console.WriteLine($"read_retained_message: {kept.StructuredContent!.Value.GetRawText()}");
return 0;
