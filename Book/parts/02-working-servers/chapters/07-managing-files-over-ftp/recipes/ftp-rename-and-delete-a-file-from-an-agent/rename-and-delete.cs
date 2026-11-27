// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent renames and deletes files on an FTP server it starts with surl-mcp, sending RNFR, RNTO and DELE
// with curl -Q through curl-mcp, and confirms the result with list_files, over the official MCP SDK.
// Run: dotnet run rename-and-delete.cs [-- delete|taken|missing|uploads-off|no-login]
//   (no argument) rename draft.txt to final.txt, the replies, the folder listing and the store
//   delete        a delete sent before the listing, and a delete sent after it
//   taken         a rename onto a name that is taken
//   missing       a rename of, and a delete of, a file that is not there, and a rename with no source
//   uploads-off   a server started without --allow-uploads
//   no-login      a server started without --allow-anonymous
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "rename";

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

// surl-mcp: an FTP server on a free port holding four files in memory. Each option is surl's own, passed
// in surl's own words, and each refusal run leaves one out. --list-directories lets the listing through.
var surlArguments = new List<string> { "--list-directories" };
if (which != "no-login") surlArguments.Add("--allow-anonymous");
if (which != "uploads-off") surlArguments.Add("--allow-uploads");

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string>
    {
        ["draft.txt"] = "draft.txt\n", ["scratch.txt"] = "scratch.txt\n", ["keep.txt"] = "keep me\n", ["taken.txt"] = "taken.txt\n",
    },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "rename":
        JsonElement renamed = await Curl(["-s", "-v", "-l", "-Q", "RNFR draft.txt", "-Q", "RNTO final.txt", baseUrl]);
        Console.WriteLine($"listing: {Lines(renamed, "standardOutput")}");
        Console.WriteLine($"replies: {string.Join(", ", Replies(renamed))}");
        await Store();
        break;
    case "delete":
        // A command without a leading dash is sent before the transfer, one with a dash after it.
        JsonElement before = await Curl(["-s", "-l", "-Q", "DELE scratch.txt", baseUrl]);
        Console.WriteLine($"listing: {Lines(before, "standardOutput")}");
        JsonElement after = await Curl(["-s", "-l", "-Q", "-DELE draft.txt", baseUrl]);
        Console.WriteLine($"listing: {Lines(after, "standardOutput")}");
        await Store();
        break;
    case "taken":
        await Curl(["-s", "-o", "listing.txt", "-Q", "RNFR keep.txt", "-Q", "RNTO taken.txt", baseUrl]);
        CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/taken.txt" });
        Console.WriteLine($"taken.txt: {read.StructuredContent!.Value.GetProperty("text").GetString()!.Replace("\n", "\\n")}");
        await Store();
        break;
    case "missing":
        foreach (string[] commands in new string[][] { ["RNFR nothere.txt", "RNTO y.txt"], ["DELE nothere.txt"], ["RNTO z.txt"] })
        {
            var curlArguments = new List<string> { "-s", "-o", "listing.txt" };
            foreach (string command in commands) curlArguments.AddRange(["-Q", command]);
            curlArguments.Add(baseUrl);
            Console.WriteLine($"{commands[0]}: curl exit {(await Curl(curlArguments.ToArray())).GetProperty("exitCode")}");
        }

        break;
    default:
        Console.WriteLine($"{which}: curl exit {(await Curl(["-s", "-o", "listing.txt", "-Q", "RNFR draft.txt", "-Q", "RNTO final.txt", baseUrl])).GetProperty("exitCode")}");
        await Store();
        break;
}

return 0;

// One curl-mcp call: curl with these arguments. The answer is text, JSON.
async Task<JsonElement> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// A stream of the answer as names on one line.
static string Lines(JsonElement answer, string stream) =>
    string.Join(" ", answer.GetProperty(stream).GetProperty("content").GetString()!.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')));

// The replies curl -v shows on standard error to the two commands: 350 to RNFR and 250 to RNTO.
static IEnumerable<string> Replies(JsonElement answer) =>
    answer.GetProperty("standardError").GetProperty("content").GetString()!.Split('\n')
        .Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith("< 350 ") || l.StartsWith("< 250 ")).Select(l => l[2..]);

// surl-mcp list_files: the store as the server holds it, names in order.
async Task Store()
{
    CallToolResult result = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/" });
    Console.WriteLine($"store: {string.Join(" ", result.StructuredContent!.Value.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()))}");
}
