// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts an HTTP server that accepts uploads from one account with surl-mcp, puts a file into it with
// curl-mcp and reads it back from the store with read_file, over the official MCP SDK.
// Run: dotnet run uploads.cs [-- replace|refused|uploads-off|no-account|anonymous|events]
//   (no argument) a Digest upload, and the file read back from the store
//   replace       upload the same path twice
//   refused       uploads that fail: a missing folder, no login, a wrong password, Basic
//   uploads-off   a server started without --allow-uploads
//   no-account    uploads on and no account
//   anonymous     uploads on and --allow-anonymous, no account
//   events        the store change the upload made
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "upload";

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

// surl-mcp: an HTTP server on a free port. Each option is surl's own, passed in surl's own words; the
// refusal runs leave the uploads, or the account, out.
var surlArguments = new List<string>();
if (which != "uploads-off") surlArguments.Add("--allow-uploads");
if (which is not ("no-account" or "anonymous")) surlArguments.AddRange(["--user", "alice:s3cret"]);
if (which == "anonymous") surlArguments.Add("--allow-anonymous");

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

string[] digest = ["--digest", "-u", "alice:s3cret"];
switch (which)
{
    case "replace":
        await Put("first", digest, "notes.txt");
        await Put("second", digest, "notes.txt");
        break;
    case "refused":
        await Put("missing folder", digest, "nowhere/notes.txt");
        await Put("no login", [], "anon.txt");
        await Put("wrong password", ["--digest", "-u", "alice:wrong"], "w.txt");
        await Put("basic", ["-u", "alice:s3cret"], "b.txt");
        await Read("/anon.txt");
        break;
    case "uploads-off":
        await Put("upload", digest, "notes.txt");
        break;
    case "no-account" or "anonymous":
        await Put("upload", [], "notes.txt");
        await Read("/notes.txt");
        break;
    case "events":
        await Put("upload", digest, "notes.txt");
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = serverId, ["kinds"] = new[] { "store_changed" },
        });
        foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
        {
            Console.WriteLine($"store_changed: {e.GetProperty("change")} {e.GetProperty("name")}");
        }

        break;
    default:
        await Put("upload", digest, "notes.txt");
        await Read("/notes.txt");
        break;
}

return 0;

// One curl-mcp call: curl -T, with the login options given, answering the status code curl wrote.
async Task Put(string label, string[] login, string path)
{
    var curlArguments = new List<string> { "-s", "-o", "put.out", "-w", "%{http_code}" };
    curlArguments.AddRange(login);
    curlArguments.AddRange(["-T", "notes.txt", baseUrl + path]);
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    Console.WriteLine($"{label}: {answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()}");
}

// surl-mcp read_file: what the store holds at a path, or the error when it holds nothing.
async Task Read(string path)
{
    CallToolResult result = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["path"] = path,
    });
    if (result.IsError == true)
    {
        Console.WriteLine($"read_file {path}: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
        return;
    }

    JsonElement file = result.StructuredContent!.Value;
    Console.Write($"read_file {path}: {file.GetProperty("byte_count")} bytes, {file.GetProperty("content_type").GetString()}, {file.GetProperty("text").GetString()}");
}
