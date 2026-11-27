// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent uploads a report into a folder path that does not exist yet on an FTP server it starts with
// surl-mcp, using curl --ftp-create-dirs through curl-mcp, and checks each folder with list_files, over the
// official MCP SDK.
// Run: dotnet run make-folders.cs [-- without|commands|uploads-off|no-login]
//   (no argument) the upload with --ftp-create-dirs, and the store listed level by level
//   without       the same upload without it
//   commands      two uploads into another new folder, and the commands the server received
//   uploads-off   a server started without --allow-uploads, with --ftp-create-dirs
//   no-login      a server started without --allow-anonymous
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "create";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();

// The report to upload is a local file, which curl -T reads from the folder curl-mcp is confined to.
File.WriteAllText("report.txt", "totals\n");

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

// surl-mcp: an FTP server on a free port holding one file in the 2026 folder. Each loosening option is
// surl's own, passed in surl's own words, and each refusal run leaves one out.
var surlArguments = new List<string>();
if (which != "no-login") surlArguments.Add("--allow-anonymous");
if (which != "uploads-off") surlArguments.Add("--allow-uploads");

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["2026/old.txt"] = "already here\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "create":
        Console.WriteLine($"upload exit: {await Upload("2026/11/12/report.txt", create: true)}");
        foreach (string folder in new[] { "/", "/2026", "/2026/11", "/2026/11/12" })
        {
            await List(folder);
        }

        break;
    case "commands":
        await Upload("2026/11/13/report.txt", create: true);
        await Upload("2026/11/13/report.txt", create: true);
        Console.WriteLine($"commands: {string.Join(", ", await Commands(2))}");
        break;
    default:
        Console.WriteLine($"{which}: upload exit {await Upload("2026/11/12/report.txt", create: which != "without")}");
        await List("/");
        break;
}

return 0;

// One curl-mcp call: curl -T report.txt, with --ftp-create-dirs when asked. Returns curl's exit code.
async Task<int> Upload(string path, bool create)
{
    var curlArguments = new List<string> { "-s", "-T", "report.txt" };
    if (create) curlArguments.Add("--ftp-create-dirs");
    curlArguments.Add(baseUrl + path);
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("exitCode").GetInt32();
}

// surl-mcp list_files: one folder of the server's store, names in order, a folder's with a slash after it.
async Task List(string folder)
{
    CallToolResult result = await surl.CallToolAsync("list_files", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["path"] = folder,
    });
    JsonElement entries = result.StructuredContent!.Value.GetProperty("entries");
    Console.WriteLine($"{folder}: {string.Join(" ", entries.EnumerateArray().Select(e => e.GetProperty("name").GetString() + (e.GetProperty("kind").GetString() == "folder" ? "/" : "")))}");
}

// surl-mcp get_exchanges: the commands that make folders or store files, in order, with the names they carried.
// The record can trail curl by a moment, so ask again until it holds a STOR per upload, for up to 10 s.
async Task<List<string>> Commands(int uploads)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    List<string> commands = await Recorded();
    while (commands.Count(c => c.StartsWith("STOR ")) < uploads && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
        commands = await Recorded();
    }

    return commands;
}

async Task<List<string>> Recorded()
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
    return result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(exchange => exchange.GetProperty("chunks").EnumerateArray())
        .Where(chunk => chunk.GetProperty("direction").GetString() == "received")
        .Select(chunk => chunk.GetProperty("data").GetString()!.Split("\r\n")[0])
        .Where(line => line.StartsWith("MKD ") || line.StartsWith("STOR "))
        .ToList();
}
