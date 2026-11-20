// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side of the conversation, as a C# file-based app: `dotnet run program.cs -- allow`
// (also: closed, small). It starts surl-mcp and curl-mcp over standard input and output with the
// official MCP SDK, calls their tools as an agent would, and prints what the recipe's claims check.
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// allow: uploads on. closed: the default, read-only. small: uploads on, but only 10 bytes of them.
string mode = args[0];

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

// surl-mcp: one server, two listeners on the folder site-<mode>. Its options go in as surl's own words:
// the directory, the account, NTLMv1 for SMB and Digest for HTTP, and the loosenings under test.
var serverOptions = new List<string> { "--directory", "site-" + mode, "-u", "alice:s3cret", "--auth", "ntlmv1,digest" };
if (mode != "closed") serverOptions.Add("--allow-uploads");
if (mode == "small") serverOptions.AddRange(["--max-filesize", "10"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smb://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = serverOptions,
});
JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string smb = bound[0].GetString()! + "docs/report.txt";
string http = bound[1].GetString()! + "docs/report.txt";

// curl-mcp's curl tool takes curl's own arguments: -T uploads a file from the confined folder.
await Curl("upload", "-s", "-S", "-u", "alice:s3cret", "-T", "report.txt", smb);
if (mode == "allow")
{
    await Curl("read over HTTP", "-s", "--digest", "-u", "alice:s3cret", http);
    await Curl("read over SMB", "-s", "-S", "-u", "alice:s3cret", smb);
    await Curl("upload again", "-s", "-S", "-u", "alice:s3cret", "-T", "report2.txt", smb);
    await Curl("read over SMB", "-s", "-S", "-u", "alice:s3cret", smb);
}
else
{
    await Curl("read over SMB", "-s", "-S", "-u", "alice:s3cret", smb);
}
return 0;

// One curl run: prints the exit code, then the first line of standard output or standard error.
async Task Curl(string label, params string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    string text = Content(root, "standardOutput") ?? Content(root, "standardError") ?? "";
    Console.WriteLine($"{label}: exit {root.GetProperty("exitCode").GetInt32()} {text.Trim()}".TrimEnd());
}

static string? Content(JsonElement root, string stream) =>
    root.GetProperty(stream).ValueKind == JsonValueKind.Object && root.GetProperty(stream).GetProperty("content").GetString() is { Length: > 0 } s ? s : null;
