// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a two-word glossary over DICT with surl-mcp and looks a word up with curl-mcp's curl tool.
// Run: dotnet run look-up.cs [-- dict-path]   (default d:surly; try m:surl or d:cheerful)
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string what = args.Length > 0 ? args[0] : "d:surly";

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

// surl-mcp: a DICT server on a free port. Each file is a headword: its name the word, its text the definition.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "dict://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string>
    {
        ["surly"] = "bad-tempered and unfriendly\n",
        ["surl"] = "the server that answers curl\n",
    },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp: the curl tool runs curl's own command line in-process, and answers in text, as JSON.
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", url + what } });
JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string reply = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;

// DICT ends its lines with CR LF; the program prints them with plain new lines.
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
Console.WriteLine($"lines end in CR LF: {(reply.EndsWith("\r\n") ? "yes" : "no")}");
Console.Write(reply.Replace("\r\n", "\n"));
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
