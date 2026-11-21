// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side of the conversation, as a C# file-based app: `dotnet run program.cs -- m:ap:*:prefix`.
// It starts surl-mcp and curl-mcp over standard input and output with the official MCP SDK, asks for the
// headwords that match, picks the first, and defines it.
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string question = args[0];

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
        ["apple"] = "a round fruit\n",
        ["apricot"] = "a small orange fruit\n",
        ["banana"] = "a long yellow fruit\n",
    },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The question first: the list of matches, or the server's refusal.
string reply = await Ask(question);
Console.Write(reply);

// Then the pick: the first word of the list, if there is one, is defined.
Match first = Regex.Match(reply, "^surl \"(.+)\"$", RegexOptions.Multiline);
if (first.Success)
{
    string word = first.Groups[1].Value;
    Console.WriteLine($"picked: {word}");
    Console.Write(await Ask($"d:{word}"));
}
else
{
    Console.WriteLine("nothing to pick");
}
return 0;

// curl-mcp: the curl tool runs curl's own command line in-process and answers in text, as JSON.
// DICT ends its lines with CR LF; they are printed as plain new lines.
async Task<string> Ask(string path)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", url + path } });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string text = answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n");
    return $"exitCode: {answer.GetProperty("exitCode")}\n{text}";
}
