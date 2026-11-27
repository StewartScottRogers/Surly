// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side of the conversation, as a C# file-based app: `dotnet run program.cs -- SCENARIO`.
// It starts surl-mcp and curl-mcp from PATH over standard input and output with the official
// MCP SDK, calls their tools as an agent would, and prints what the recipe's claims check.
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

// surl-mcp: two Gopher servers on free ports holding the same tree; only one lists directories.
var files = new Dictionary<string, string>
{
    ["hello.txt"] = "hello from surl\n",
    ["notes/today.txt"] = "nothing happened\n",
    ["notes/archive/2025.txt"] = "old news\n",
};
async Task<string> Start(string[] arguments)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "gopher://127.0.0.1:0" }, ["arguments"] = arguments, ["files"] = files,
    });
    return started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
}
string listing = await Start(["--list-directories"]);
string bare = await Start([]);

// curl-mcp: one selector in, one answer out. In a Gopher URL the first character after the slash is
// the item type; it is not sent, and the rest is the selector. Gopher has no status: the body is all.
async Task<string> Get(string baseUrl, string path)
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = baseUrl + path });
    using JsonDocument answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("body").GetProperty("content").GetString()!;
}

// A menu is lines of type+name, selector, host, port, tab-separated, ended by a line holding a dot.
// Print them without the port, which changes on every run.
void Show(string menu)
{
    foreach (string line in menu.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        Console.WriteLine(line.Replace($"\t{new Uri(listing).Port}", "\tPORT").Replace($"\t{new Uri(bare).Port}", "\tPORT"));
}

switch (args[0])
{
    case "walk":
        // From the root menu, follow the first directory entry of each menu, then fetch a file.
        string selector = "", menu;
        while (true)
        {
            menu = await Get(listing, $"1{selector}");
            string? next = menu.Split("\r\n").Where(l => l.StartsWith('1')).Select(l => l.Split('\t')[1]).FirstOrDefault();
            if (next is null) break;
            Console.WriteLine($"menu {(selector == "" ? "/" : selector)} -> {next}");
            selector = next;
        }
        string file = menu.Split("\r\n").Where(l => l.StartsWith('0')).Select(l => l.Split('\t')[1]).First();
        Console.WriteLine($"file {file}");
        Console.Write(await Get(listing, $"0{file}"));
        break;
    case "folder-menu": Show(await Get(listing, "1/notes")); break;
    case "deepest-menu": Show(await Get(listing, "1/notes/archive")); break;
    case "file-by-selector": Console.Write(await Get(listing, "0/notes/archive/2025.txt")); break;
    case "trailing-slash": Console.WriteLine(await Get(listing, "1/notes/") == await Get(listing, "1/notes")); break;
    case "type-is-not-checked":
        Console.Write(await Get(listing, "1/hello.txt"));
        Show(await Get(listing, "0/notes/archive"));
        break;
    case "missing-folder": Show(await Get(listing, "1/notes/nope")); break;
    case "missing-file": Show(await Get(listing, "0/notes/archive/2026.txt")); break;
    case "listing-off-no-walk":
        Show(await Get(bare, "1"));
        Show(await Get(bare, "1/notes"));
        break;
    case "listing-off-file-served": Console.Write(await Get(bare, "0/hello.txt")); break;
}
return 0;
