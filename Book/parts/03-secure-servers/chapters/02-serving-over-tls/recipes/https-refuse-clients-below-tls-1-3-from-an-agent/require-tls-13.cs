// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts an HTTPS server that takes TLS 1.3 only with surl-mcp, then runs curl against it
// twice with curl-mcp's curl tool: once as a TLS 1.2 client, once as a TLS 1.3 client.
// Run: dotnet run require-tls-13.cs -- <lowest>
//   1.3     the server takes TLS 1.3 only (the default)
//   1.2     the server keeps surl's default range, TLS 1.2 and 1.3
//   empty   the server is asked for nothing below 1.3 and nothing above 1.2
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string lowest = args.Length > 0 ? args[0] : "1.3";

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

// surl-mcp: any option surl has goes in arguments, one list item each.
var serverArguments = new List<string> { "--cert", "server.pem", "--key", "server.key" };
if (lowest != "1.2") serverArguments.Add("--tlsv1.3");
if (lowest == "empty") serverArguments.AddRange(["--tls-max", "1.2"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = serverArguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
if (started.IsError == true)
{
    Console.WriteLine($"start_server refused: {started.Content.OfType<TextContentBlock>().Single().Text.Split('\n')[0]}");
    return 2;
}

string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp's curl tool takes curl's arguments, one list item each. --cacert trusts the certificate.
await Run("TLS 1.2 client", ["--tls-max", "1.2", "--cacert", "server.pem", "-s", url + "hello.txt"]);
await Run("TLS 1.3 client", ["--tlsv1.3", "--cacert", "server.pem", "-s", url + "hello.txt"]);
return 0;

async Task Run(string who, string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement output = answer.GetProperty("standardOutput");
    string text = output.ValueKind == JsonValueKind.Null ? "" : output.GetProperty("content").GetString()!.Trim();
    Console.WriteLine($"{who}: exitCode {answer.GetProperty("exitCode").GetRawText()} {text}".TrimEnd());
}
