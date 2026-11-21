// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an app that writes temporary files over FTP against a surl-mcp server, using curl-mcp's curl tool
// as the app, and names each temporary file left behind with surl-mcp's list_files. Run: dotnet run program.cs -- <case>
//   clean            the app deletes both temporary files, the check passes
//   kept             what the folder holds after a clean run
//   leaky-app        the app deletes only the first, and still finishes
//   leftover         the same app, then the check: it fails and names the file
//   delete-missing   a cleanup of a file the server does not have
//   client-listing   a server that does not allow listing: curl's listing
//   server-listing   the same server: list_files
//   uploads-off      a server that takes no uploads: the app
//   nothing-written  the same server: the check
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args[0];
bool uploadsOff = which is "uploads-off" or "nothing-written";
bool noListing = which is "client-listing" or "server-listing";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. scratch.txt is what the app uploads.
string here = Directory.GetCurrentDirectory();
File.WriteAllText("scratch.txt", "scratch\n");
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

// surl-mcp: the stand-in, with no login, and a folder work/ holding one file the app must not remove. A client can list
// a folder only with --list-directories; list_files, the agent's own view of the store, needs no option.
var serverArguments = new List<string> { "--allow-anonymous" };
if (!uploadsOff) serverArguments.Add("--allow-uploads");
if (!noListing) serverArguments.Add("--list-directories");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = serverArguments,
    ["files"] = new Dictionary<string, string> { ["work/keep.txt"] = "kept\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string folder = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "work/";

switch (which)
{
    case "clean":
        await Upload();
        await Cleanup("a.tmp", "b.tmp");
        await Check();
        break;
    case "kept":
        await Upload();
        await Cleanup("a.tmp", "b.tmp");
        Console.WriteLine($"folder holds: {string.Join(" ", await Names())}");
        break;
    case "leaky-app":
        await Upload();
        await Cleanup("a.tmp");
        break;
    case "leftover":
        await Upload();
        await Cleanup("a.tmp");
        await Check();
        break;
    case "delete-missing":
        await Cleanup("gone.tmp");
        break;
    case "client-listing":
        await Curl("client listing", ["-l", folder]);
        break;
    case "server-listing":
        Console.WriteLine($"list_files /work: {string.Join(" ", await Names())}");
        break;
    case "uploads-off":
        await Upload();
        break;
    default:
        await Check();
        break;
}

return 0;

// The app: two uploads in one curl command, as it does while it works.
Task Upload() => Curl("app upload", ["-T", "scratch.txt", folder + "a.tmp", "-T", "scratch.txt", folder + "b.tmp"]);

// The app: -Q with a leading dash sends each command after the transfer, which is a listing of the folder, since a
// command needs a transfer to ride on.
Task Cleanup(params string[] names) =>
    Curl("app cleanup", ["-l", .. names.SelectMany(n => new[] { "-Q", $"-DELE {n}" }), folder]);

// The check: the names in the store, as list_files answers them, with no client and no listing option.
async Task Check()
{
    string[] left = (await Names()).Where(n => n.EndsWith(".tmp")).ToArray();
    Console.WriteLine(left.Length == 0 ? "clean" : $"leftover: {string.Join(" ", left)}");
}

async Task<string[]> Names()
{
    CallToolResult listed = await surl.CallToolAsync("list_files", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["path"] = "/work",
    });
    return listed.StructuredContent!.Value.GetProperty("entries").EnumerateArray()
        .Select(e => e.GetProperty("name").GetString()!).Order(StringComparer.Ordinal).ToArray();
}

// The curl tool runs curl's own arguments, one list item each, with no shell; -sS hides the progress meter and keeps
// the error text. A refusal is an answer with a non-zero exitCode, not a failure of the call.
async Task Curl(string what, string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS" }.Concat(curlArguments).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement error = answer.GetProperty("standardError");
    string said = error.ValueKind == JsonValueKind.Null ? "" : error.GetProperty("content").GetString()!.Trim();
    Console.WriteLine($"{what}: exit {answer.GetProperty("exitCode")}{(said == "" ? "" : $" {said}")}");
}
