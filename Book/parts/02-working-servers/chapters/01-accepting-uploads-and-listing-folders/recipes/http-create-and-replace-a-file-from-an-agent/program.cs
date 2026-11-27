// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent uploads a file, replaces it and tries a folder that is not there, with curl-mcp's curl
// tool, and checks each result with surl-mcp's read_file.
// Run: dotnet run program.cs -- <case>
//   uploads     create, replace, then a missing folder (the default)
//   fail        the missing folder again, with -f
//   read-only   the first upload to a server started without --allow-uploads
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "uploads";

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

// surl-mcp: one account, uploads on unless read-only, and a drafts folder in memory.
var arguments = new List<string> { "--user", "alice:s3cret" };
if (which != "read-only")
{
    arguments.Add("--allow-uploads");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["drafts/outline.txt"] = "an outline\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

bool failed = which switch
{
    "fail" => await Upload("report.txt", "/archive/report.txt", "-f"),
    "read-only" => await Upload("report.txt", "/report.txt"),
    _ => await Upload("report.txt", "/report.txt")
        | await Upload("report-v2.txt", "/report.txt")
        | await Upload("report.txt", "/archive/report.txt"),
};
return failed ? 1 : 0;

// One upload with the curl tool (curl -T, logged in with Digest), then read_file on the same path.
// Answers whether the curl call was a tool error.
async Task<bool> Upload(string file, string path, params string[] extra)
{
    var curlArgs = new List<string> { "-s", "-w", "%{http_code}", "--digest", "-u", "alice:s3cret", "-T", file };
    curlArgs.AddRange(extra);
    curlArgs.Add(url + path.TrimStart('/'));
    CallToolResult put = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    JsonElement answer = JsonDocument.Parse(put.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    // -w writes the status after any body, so it is the last line; a refusal's body is one line before it.
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    int cut = output.LastIndexOf('\n');
    string body = cut < 0 ? "" : output[..cut];
    Console.Write($"PUT {path}: {output[(cut + 1)..]}{(body.Length > 0 ? $" (body: {body})" : "")}");
    Console.Write($", exitCode {answer.GetProperty("exitCode")}, tool error {(put.IsError == true ? "yes" : "no")}");

    // surl-mcp answers read_file in structured content, or with a tool error when there is no file.
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = path });
    string holds = read.IsError == true
        ? read.Content.OfType<TextContentBlock>().Single().Text
        : read.StructuredContent!.Value.GetProperty("text").GetString()!.TrimEnd('\n');
    Console.WriteLine($"; read_file: {holds}");
    return put.IsError == true;
}
