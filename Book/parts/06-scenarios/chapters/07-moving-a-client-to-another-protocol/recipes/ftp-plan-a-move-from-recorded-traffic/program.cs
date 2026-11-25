// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side of the conversation, as a C# file-based app: `dotnet run program.cs`.
// It starts surl-mcp and curl-mcp from PATH over standard input and output with the official
// MCP SDK, calls their tools as an agent would, and prints what the recipe's claims check.
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

// surl-mcp: a server on a free port, holding one file in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp: fetch it. curl-mcp answers in text, as JSON.
CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt" });
using JsonDocument answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text);
Console.WriteLine(answer.RootElement.GetProperty("statusCode").GetInt32());
Console.Write(answer.RootElement.GetProperty("body").GetProperty("content").GetString());

// What the program prints and the exit code it returns are what recipe.yml checks.
return fetched.IsError == true ? 1 : 0;
