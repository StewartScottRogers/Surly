// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks a URL a user gave it with curl-mcp's parse_url against the server it started
// with surl-mcp, and fetches with transfer only when scheme, host and port all match.
// Run: dotnet run check-url.cs -- <url>
//   PORT in the URL is replaced by the port the server bound, as if the user had typed it.
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

// surl-mcp: the server the user means, on a free port.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
var bound = new Uri(server.GetProperty("bound_urls")[0].GetString()!);
string given = args[0].Replace("PORT", bound.Port.ToString());

// parse_url: DefaultPort fills in the scheme's port when the URL leaves it out.
CallToolResult parsed = await curl.CallToolAsync("parse_url", new Dictionary<string, object?>
{
    ["url"] = given,
    ["flags"] = new[] { "DefaultPort" },
});
JsonElement answer = JsonDocument.Parse(parsed.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string verdict;
if (parsed.IsError == true)
{
    verdict = $"not a URL: {answer.GetProperty("failure").GetProperty("text")}";
}
else
{
    JsonElement p = answer.GetProperty("parts");
    string scheme = p.GetProperty("scheme").GetString()!, host = p.GetProperty("host").GetString()!, port = p.GetProperty("port").GetString()!;
    Console.WriteLine($"scheme {scheme}, host {host}, port {(port == bound.Port.ToString() ? "PORT" : port)}, path {p.GetProperty("path")}");
    verdict = scheme != bound.Scheme ? $"wrong scheme: {scheme}, the server speaks {bound.Scheme}"
        : host != bound.Host ? $"wrong host: {host}, the server is on {bound.Host}"
        : port != bound.Port.ToString() ? $"wrong port: {port}, the server is on PORT"
        : "ok";
}

Console.WriteLine(verdict);
if (verdict == "ok")
{
    // Only now is a request made.
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = given });
    JsonElement got = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"statusCode: {got.GetProperty("statusCode")}");
}

// What the server saw: list_servers counts the exchanges each server has had.
CallToolResult listed = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
Console.WriteLine($"exchanges on the server: {listed.StructuredContent!.Value.GetProperty("servers")[0].GetProperty("exchange_count")}");
return verdict == "ok" ? 0 : 1;
