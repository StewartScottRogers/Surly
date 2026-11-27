// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent lists and makes SFTP folders on servers it starts with surl-mcp, using curl-mcp, over the
// official MCP SDK. Run: dotnet run program.cs
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

// Three servers: listings and folders on, listings only, and neither.
(string id, string url) main = await Start("--list-directories", "--allow-uploads");
(string id, string url) listingOnly = await Start("--list-directories");
(string id, string url) closed = await Start();

await Ask("list", main.url, "-l");
await Ask("mkdir /reports", main.url, "-l", "-Q", "mkdir /reports");
await Ask("list again", main.url, "-l");
await Ask("mkdir /reports", main.url, "-l", "-Q", "mkdir /reports");
await Ask("-mkdir /archive", main.url, "-l", "-Q", "-mkdir /archive");
await Ask("list last", main.url, "-l");
await Ask("listing-only mkdir", listingOnly.url, "-l", "-Q", "mkdir /reports");
await Ask("closed list", closed.url, "-l");

// surl-mcp can read the server's store back: the folders are in what the server holds.
CallToolResult stored = await surl.CallToolAsync("list_files", new Dictionary<string, object?>
{
    ["server_id"] = main.id, ["path"] = "/",
});
Console.WriteLine($"list_files: {stored.Content.OfType<TextContentBlock>().Single().Text}");
return 0;

// surl-mcp: an SFTP server on a free port with a throwaway host key, one account, one file in memory.
async Task<(string, string)> Start(params string[] options)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "sftp://127.0.0.1:0" },
        ["arguments"] = new[] { "--throwaway-hostkey", "--user", "alice:s3cret" }.Concat(options).ToArray(),
        ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    });
    JsonElement server = started.StructuredContent!.Value;
    return (server.GetProperty("server_id").GetString()!, server.GetProperty("bound_urls")[0].GetString()!);
}

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. -k skips the host key check.
async Task Ask(string label, string url, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-k", "-u", "alice:s3cret" }.Concat(more).Append(url).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string names = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "").Replace('\n', ' ').TrimEnd();
    string error = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    Console.WriteLine($"{label}: [{names}] exit {answer.RootElement.GetProperty("exitCode")} {error}".TrimEnd());
}
