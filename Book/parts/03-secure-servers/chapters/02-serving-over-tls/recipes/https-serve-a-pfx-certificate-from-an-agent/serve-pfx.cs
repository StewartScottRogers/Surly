// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a PFX certificate with surl-mcp and fetches over HTTPS with curl-mcp's transfer tool.
// Run: dotnet run serve-pfx.cs -- <passphrase>
//   s3cret   the passphrase the PFX was made with
//   wrong    a passphrase it was not made with
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string passphrase = args[0];

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. It holds
// server.pfx and server.pem, which openssl wrote.
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
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pfx", "--cert-type", "P12", "--pass", passphrase },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
if (started.IsError == true)
{
    JsonElement refusal = started.StructuredContent!.Value;
    Console.WriteLine($"exit_code: {refusal.GetProperty("exit_code")} ({refusal.GetProperty("exit_code_name")})");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    return 1;
}

string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp's transfer tool: caFile trusts server.pem, the same certificate as a PEM file,
// as curl --cacert does.
CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?>
{
    ["url"] = url + "hello.txt",
    ["caFile"] = "server.pem",
});
JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"statusCode: {answer.GetProperty("statusCode").GetRawText()}");
Console.Write(answer.GetProperty("body").GetProperty("content").GetString());
return 0;
