// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent appends lines to a file on an FTP server it starts with surl-mcp, using curl --append through
// curl-mcp, and reads the whole file back with read_file, over the official MCP SDK.
// Run: dotnet run append-log.cs [-- replace|edges|uploads-off|too-big|no-login]
//   (no argument) two appends, the file after each, and the commands the server received
//   replace       the same upload without --append, which replaces the file
//   edges         an append to a file that is not there, and to one with no final newline
//   uploads-off   a server started without --allow-uploads
//   too-big       a server started with --max-filesize 15
//   no-login      a server started without --allow-anonymous
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "append";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();

// The line to send is a local file, which curl -T uploads from the folder curl-mcp is confined to.
File.WriteAllText("line.txt", "second line\n");

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

// surl-mcp: an FTP server on a free port with three files in memory. Each loosening option is surl's own,
// passed in surl's own words, and each refusal run leaves one out.
var surlArguments = new List<string>();
if (which != "no-login") surlArguments.Add("--allow-anonymous");
if (which != "uploads-off") surlArguments.Add("--allow-uploads");
if (which == "too-big") surlArguments.AddRange(["--max-filesize", "15"]);

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string>
    {
        ["log.txt"] = "first line\n",
        ["replace.txt"] = "old contents\n",
        ["no-newline.txt"] = "no newline",
    },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "append":
        await Upload("log.txt", append: true);
        Console.WriteLine($"log.txt: {await Read("log.txt")}");
        await Upload("log.txt", append: true);
        Console.WriteLine($"log.txt: {await Read("log.txt")}");
        Console.WriteLine($"commands: {string.Join(", ", await Commands(2))}");
        break;
    case "replace":
        await Upload("replace.txt", append: false);
        Console.WriteLine($"replace.txt: {await Read("replace.txt")}");
        Console.WriteLine($"commands: {string.Join(", ", await Commands(1))}");
        break;
    case "edges":
        await Upload("fresh.txt", append: true);
        Console.WriteLine($"fresh.txt: {await Read("fresh.txt")}");
        await Upload("no-newline.txt", append: true);
        Console.WriteLine($"no-newline.txt: {await Read("no-newline.txt")}");
        break;
    default:
        Console.WriteLine($"{which}: curl exit {await Upload("log.txt", append: true)}");
        Console.WriteLine($"log.txt: {await Read("log.txt")}");
        break;
}

return 0;

// One curl-mcp call: curl -T line.txt, with --append when asked. Returns curl's exit code.
async Task<int> Upload(string name, bool append)
{
    var curlArguments = new List<string> { "-s", "-T", "line.txt" };
    if (append) curlArguments.Add("--append");
    curlArguments.Add(baseUrl + name);
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("exitCode").GetInt32();
}

// surl-mcp read_file: the file as the server holds it, with its line ends written out as \n.
async Task<string> Read(string name)
{
    CallToolResult result = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["path"] = "/" + name,
    });
    return result.StructuredContent!.Value.GetProperty("text").GetString()!.Replace("\n", "\\n");
}

// surl-mcp get_exchanges: the upload commands the server received, in order, with the names they carried.
// The record can trail curl by a moment, so ask again until it holds one per upload, for up to 10 s.
async Task<List<string>> Commands(int uploads)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    List<string> commands = await Recorded();
    while (commands.Count < uploads && DateTime.UtcNow < deadline)
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
        .Where(line => line.StartsWith("APPE ") || line.StartsWith("STOR "))
        .ToList();
}
