// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes and removes folders on an FTP server it starts with surl-mcp, sending MKD and RMD as quote
// commands through curl-mcp, and reads the server's answers with get_exchanges and its store with list_files,
// over the official MCP SDK.
// Run: dotnet run folders.cs -- SCENARIO
//   make            MKD reports
//   inside          MKD reports/2026, with reports there
//   exists          MKD reports, with reports there
//   missing-parent  MKD nowhere/new
//   remove          RMD archive, with archive there and empty
//   not-empty       RMD reports, with a file in it
//   clear           RMD reports, refused; the file listed and deleted; RMD reports again
//   missing         RMD of a name that is a file
//   uploads-off     MKD on a server started without --allow-uploads
//   uploads-off-rmd RMD on that server, of a folder with a file in it
//   no-login        MKD on a server started without --allow-anonymous
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "make";
bool seedFile = scenario is "not-empty" or "clear" or "uploads-off-rmd";

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

// surl-mcp: an FTP server on a free port. Each option is surl's own, in surl's own words, and each refusal run
// leaves one out. --list-directories lets the FTP server list folders, which curl's quote rides on.
var surlArguments = new List<string> { "--list-directories" };
if (scenario != "no-login") surlArguments.Add("--allow-anonymous");
if (!scenario.StartsWith("uploads-off")) surlArguments.Add("--allow-uploads");
var files = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" };
if (seedFile) files["reports/note.txt"] = "a note\n";

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = files,
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The folders that are there at the start, made with MKD as any client would, before the one that is tried.
string[] before = scenario switch
{
    "inside" or "exists" => ["MKD reports"],
    "remove" => ["MKD archive"],
    _ => [],
};
if (before.Length > 0)
{
    await Send(before);
}

if (scenario == "clear")
{
    // What an agent does with the refusal: read why, list the folder, delete what is in it, try again.
    (int first, string firstReplies) = await Try(["RMD reports"]);
    Console.WriteLine($"first RMD: exit {first}; {firstReplies}");
    foreach (string file in await Names("/reports"))
    {
        await Send([$"DELE reports/{file}"]);
        Console.WriteLine($"deleted: {file}");
    }

    (int second, string secondReplies) = await Try(["RMD reports"]);
    Console.WriteLine($"second RMD: exit {second}; {secondReplies}");
    Console.WriteLine($"store: {await Tree("/")}");
    return 0;
}

string[] quotes = scenario switch
{
    "make" or "uploads-off" or "no-login" or "exists" => ["MKD reports"],
    "inside" => ["MKD reports/2026"],
    "missing-parent" => ["MKD nowhere/new"],
    "remove" => ["RMD archive"],
    "not-empty" or "uploads-off-rmd" => ["RMD reports"],
    "missing" => ["RMD hello.txt"],
    _ => throw new ArgumentException($"unknown scenario {scenario}"),
};
(int exit, string replies) = await Try(quotes);
Console.WriteLine($"{scenario}: exit {exit}; {replies}; store: {await Tree("/")}");
return 0;

// One curl-mcp call: curl -Q for each command, on the root listing, thrown away. -s keeps the progress meter out
// of the answer. The answer is JSON text with the exit code; a refused quote is exit 21, a refused login 67.
async Task<int> Send(string[] commands)
{
    var curlArguments = new List<string> { "-s", "-o", "listing.txt" };
    foreach (string command in commands)
    {
        curlArguments.AddRange(["-Q", command]);
    }

    curlArguments.Add(baseUrl);
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("exitCode").GetInt32();
}

// A command and the server's answer to it, read from the last exchange.
async Task<(int Exit, string Replies)> Try(string[] commands)
{
    int seen = await ExchangeCount();
    int exit = await Send(commands);
    return (exit, await Replies(seen));
}

async Task<int> ExchangeCount() =>
    (await Exchanges()).GetArrayLength();

async Task<JsonElement> Exchanges()
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
    return result.StructuredContent!.Value.GetProperty("exchanges").Clone();
}

// surl-mcp get_exchanges: the server's answers that matter, a 257 created, 250 Directory, 530 or 550 line, from the
// exchange that came after the count. The record can trail curl by a moment, so ask again for up to 10 s.
async Task<string> Replies(int seen)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    string found = await Read(seen);
    while (found.Length == 0 && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
        found = await Read(seen);
    }

    return found;
}

async Task<string> Read(int seen)
{
    JsonElement exchanges = await Exchanges();
    if (exchanges.GetArrayLength() <= seen) return "";
    return string.Join(" | ", exchanges[seen].GetProperty("chunks").EnumerateArray()
        .Where(chunk => chunk.GetProperty("direction").GetString() == "sent")
        .SelectMany(chunk => chunk.GetProperty("data").GetString()!.Split("\r\n"))
        .Where(line => Regex.IsMatch(line, "^(257 \".+\" created|250 Directory|530|550)")));
}

// surl-mcp list_files: one folder of the server's store, one level, as names with a slash after a folder's.
async Task<List<string>> Names(string folder, bool filesOnly = true)
{
    CallToolResult result = await surl.CallToolAsync("list_files", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["path"] = folder,
    });
    return result.StructuredContent!.Value.GetProperty("entries").EnumerateArray()
        .Where(e => !filesOnly || e.GetProperty("kind").GetString() == "file")
        .Select(e => e.GetProperty("name").GetString()! + (e.GetProperty("kind").GetString() == "folder" ? "/" : ""))
        .ToList();
}

// Every folder and file of the store below a path.
async Task<string> Tree(string folder)
{
    var names = new List<string>();
    foreach (string entry in (await Names(folder, filesOnly: false)).Order(StringComparer.Ordinal))
    {
        string path = folder.TrimEnd('/') + "/" + entry.TrimEnd('/');
        names.Add(path[1..] + (entry.EndsWith('/') ? "/" : ""));
        if (entry.EndsWith('/'))
        {
            string inner = await Tree(path);
            if (inner.Length > 0) names.Add(inner);
        }
    }

    return string.Join(" ", names);
}
