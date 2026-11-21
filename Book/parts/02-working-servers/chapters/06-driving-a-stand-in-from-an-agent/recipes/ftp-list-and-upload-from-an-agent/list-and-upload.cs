// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent lists a folder of an FTP server it starts with surl-mcp and uploads a file into it with curl-mcp,
// then confirms with list_files and read_file, over the official MCP SDK.
// Run: dotnet run list-and-upload.cs [-- long|subfolder|listings-off|uploads-off|no-login]
//   (no argument) list names, upload notes.txt, list again, and read it back from the store
//   long          the long listing, as curl asks for it without -l
//   subfolder     list a subfolder, named with its trailing slash
//   listings-off  a server started without --list-directories
//   uploads-off   a server started without --allow-uploads
//   no-login      a server started without --allow-anonymous
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "list";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();

// The file to upload is local, and curl -T reads it from the folder curl-mcp is confined to.
File.WriteAllText("notes.txt", "uploaded by curl\n");

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

// surl-mcp: an FTP server on a free port. Each option that loosens something is surl's own, passed in
// surl's own words, and each refusal run leaves one out.
var surlArguments = new List<string>();
if (which != "no-login") surlArguments.Add("--allow-anonymous");
if (which != "listings-off") surlArguments.Add("--list-directories");
if (which != "uploads-off") surlArguments.Add("--allow-uploads");

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string>
    {
        ["hello.txt"] = "hello from surl\n", ["sub/a.txt"] = "a\n", [".hidden"] = "not for you\n",
    },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "long":
        foreach (string line in (await Curl("-s", baseUrl)).Lines)
        {
            Console.WriteLine($"  {line[0]} {line[(line.LastIndexOf(' ') + 1)..]}");
        }

        break;
    case "subfolder":
        await Show("list /sub/", "-s", "-l", baseUrl + "sub/");
        break;
    case "list":
        await Show("list /", "-s", "-l", baseUrl);
        await Show("upload", "-s", "-T", "notes.txt", baseUrl + "notes.txt");
        await Show("list /", "-s", "-l", baseUrl);
        await ListFiles("/");
        CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
        {
            ["server_id"] = serverId, ["path"] = "/notes.txt",
        });
        Console.Write($"read_file /notes.txt: {read.StructuredContent!.Value.GetProperty("byte_count")} bytes, {read.StructuredContent!.Value.GetProperty("text").GetString()}");
        break;
    default:
        await Show("list /", "-s", "-l", baseUrl);
        await Show("upload", "-s", "-T", "notes.txt", baseUrl + "notes.txt");
        break;
}

return 0;

// One curl-mcp call: the exit code, whether the tool called it an error, and the output lines.
async Task<(int Exit, bool IsError, string[] Lines)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString() ?? "";
    return (answer.RootElement.GetProperty("exitCode").GetInt32(), result.IsError == true,
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray());
}

async Task Show(string label, params string[] curlArguments)
{
    var (exit, isError, lines) = await Curl(curlArguments);
    Console.WriteLine($"{label}: exit {exit}, tool error: {(isError ? "yes" : "no")}{(lines.Length > 0 ? ", " + string.Join(" ", lines) : "")}");
}

// surl-mcp list_files: one folder of the server's store, names in order, a folder's with a slash after it.
async Task ListFiles(string folder)
{
    CallToolResult result = await surl.CallToolAsync("list_files", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["path"] = folder,
    });
    JsonElement entries = result.StructuredContent!.Value.GetProperty("entries");
    Console.WriteLine($"list_files {folder}: {string.Join(" ", entries.EnumerateArray().Select(e => e.GetProperty("name").GetString() + (e.GetProperty("kind").GetString() == "folder" ? "/" : "")))}");
}
