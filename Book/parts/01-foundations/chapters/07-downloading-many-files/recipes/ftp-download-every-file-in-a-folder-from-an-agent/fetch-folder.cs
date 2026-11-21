// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent downloads an FTP folder with curl-mcp: list it, tell files from subfolders, fetch each file
// into its root and compare it with what surl-mcp holds, over the official MCP SDK.
// Run: dotnet run fetch-folder.cs [-- sub|refused|folder|vanished|hidden]
//   (no argument) the folder /pub/, three files, a subfolder and a dot file
//   sub           the subfolder, fetched by the same routine
//   refused       the same server started without --list-directories
//   folder        a subfolder asked for as if it were a file
//   vanished      a name that is not on the server
//   hidden        the dot file, asked for by name
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "pub";

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

// surl-mcp: an anonymous FTP server that lists folders (unless this run leaves that out), holding a
// folder of three files, a subfolder and a dot file.
var surlArguments = new List<string> { "--allow-anonymous" };
if (which != "refused")
{
    surlArguments.Add("--list-directories");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string>
    {
        ["pub/a.txt"] = "alpha\n",
        ["pub/b.txt"] = "bravo\n",
        ["pub/c.txt"] = "charlie\n",
        ["pub/.hidden"] = "not served\n",
        ["pub/archive/old.txt"] = "from last year\n",
    },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "pub":
        return await Download("pub");
    case "sub":
        return await Download("pub/archive");
    case "refused":
        (int code, string _) = await Curl("-s", "-l", $"{url}pub/");
        Console.WriteLine($"listing: exit {code}");
        return 0;
    default:
        string path = which switch { "folder" => "pub/archive", "hidden" => "pub/.hidden", _ => "pub/gone.txt" };
        (int failed, string _) = await Curl("-s", $"{url}{path}");
        Console.WriteLine($"{Path.GetFileName(path)}: exit {failed}");
        return 0;
}

// One curl-mcp call: the exit code and what curl wrote to standard output. curl-mcp answers in text, as JSON.
async Task<(int Code, string Out)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(), answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", ""));
}

// List the folder twice, for the names and for the types, skip the subfolders, fetch each file into
// the root, and compare it with what surl-mcp holds.
async Task<int> Download(string folder)
{
    // -l: names only, in order. Without it: the long listing, where a folder's line starts with d.
    string[] names = (await Curl("-s", "-l", $"{url}{folder}/")).Out.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    string[] folders = (await Curl("-s", $"{url}{folder}/")).Out.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.StartsWith('d')).Select(line => line[(line.LastIndexOf(' ') + 1)..]).ToArray();
    Console.WriteLine($"listing: {string.Join(" ", names)}");
    Console.WriteLine($"folders: {string.Join(" ", folders)}");

    bool allSame = true;
    foreach (string name in names)
    {
        if (folders.Contains(name))
        {
            Console.WriteLine($"skip {name}/");
            continue;
        }

        (int code, string _) = await Curl("-s", "--create-dirs", "-o", $"out/{folder}/{name}", $"{url}{folder}/{name}");
        CallToolResult held = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
        {
            ["server_id"] = serverId,
            ["path"] = $"/{folder}/{name}",
        });
        string served = held.StructuredContent!.Value.GetProperty("text").GetString()!;
        string local = File.ReadAllText($"out/{folder}/{name}");
        allSame &= local == served;
        Console.WriteLine($"get {name}: exit {code}, {(local == served ? "identical to the server's" : "DIFFERENT from the server's")}");
    }

    Console.WriteLine($"in out/{folder}: {string.Join(" ", Directory.GetFiles($"out/{folder}").Select(Path.GetFileName).Order())}");
    return allSame ? 0 : 1;
}
