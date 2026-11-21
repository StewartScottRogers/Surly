// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts one surl-mcp server that listens for FTP and HTTP, uploads a file over FTP with curl-mcp
// and fetches it over HTTP from the same server, over the official MCP SDK.
// Run: dotnet run share.cs [-- ftp-too|http-put|exchanges|uploads-off|no-login]
//   (no argument) fetch notes.txt over HTTP before and after the FTP upload, then read it from the store
//   ftp-too       after the upload, fetch the file over FTP as well
//   http-put      upload over HTTP, fetch over FTP
//   exchanges     what the server received on each of its two listeners
//   uploads-off   a server started without --allow-uploads
//   no-login      a server started without --allow-anonymous
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "share";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();

// The files to upload are local, and curl -T reads them from the folder curl-mcp is confined to.
File.WriteAllText("notes.txt", "uploaded over ftp\n");
File.WriteAllText("reply.txt", "uploaded over http\n");

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

// surl-mcp: one server, two listen URLs, one store. Each option that loosens something is surl's own,
// passed in surl's own words, and each refusal run leaves one out.
var surlArguments = new List<string>();
if (which != "no-login") surlArguments.Add("--allow-anonymous");
if (which != "uploads-off") surlArguments.Add("--allow-uploads");

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string ftp = bound[0].GetString()!;
string http = bound[1].GetString()!;
Console.WriteLine($"schemes: {new Uri(ftp).Scheme} {new Uri(http).Scheme}");

switch (which)
{
    case "http-put":
        await Show("PUT over http", "-s", "-T", "reply.txt", "-o", "reply.out", "-w", "%{http_code}", http + "reply.txt");
        await Show("GET over ftp", "-s", ftp + "reply.txt");
        break;
    case "uploads-off" or "no-login":
        await Show("STOR over ftp", "-s", "-T", "notes.txt", ftp + "notes.txt");
        break;
    default:
        await Show("GET over http", "-s", "-o", "before.out", "-w", "%{http_code}", http + "notes.txt");
        await Show("STOR then GET", "-T", "notes.txt", ftp + "notes.txt", "--next", "-s", http + "notes.txt");
        if (which == "ftp-too")
        {
            await Show("GET over ftp", "-s", ftp + "notes.txt");
        }

        CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
        {
            ["server_id"] = serverId, ["path"] = "/notes.txt",
        });
        Console.Write($"read_file /notes.txt: {read.StructuredContent!.Value.GetProperty("text").GetString()}");
        if (which == "exchanges")
        {
            foreach (string scheme in new[] { "ftp", "http" })
            {
                Console.WriteLine($"{scheme} received: {string.Join(", ", await Received(scheme))}");
            }
        }

        break;
}

return 0;

// One curl-mcp call: the exit code, whether the tool called it an error, and the output lines.
async Task Show(string label, params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString() ?? "";
    string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
    Console.WriteLine($"{label}: exit {answer.RootElement.GetProperty("exitCode")}, tool error: {(result.IsError == true ? "yes" : "no")}{(lines.Length > 0 ? ", " + string.Join(" ", lines) : "")}");
}

// surl-mcp get_exchanges, for one listener's scheme: the command or request line of each exchange that carried a file name.
async Task<List<string>> Received(string scheme)
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["scheme"] = scheme,
    });
    return result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(exchange => exchange.GetProperty("chunks").EnumerateArray())
        .Where(chunk => chunk.GetProperty("direction").GetString() == "received")
        .SelectMany(chunk => chunk.GetProperty("data").GetString()!.Split("\r\n"))
        .Where(line => line.StartsWith("STOR ") || line.StartsWith("GET /notes.txt"))
        .ToList();
}
