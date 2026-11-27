// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent shares one folder read-only and one writable through surl-mcp, then tries an upload
// into each with curl-mcp. Run: dotnet run program.cs -- <case>
//   solution       read from the read-only share, upload into it (refused), look at the folder
//   writable       the same upload into the writable share, and the event that records it
//   uploads-off    the server started without --allow-uploads
//   no-login       the server started without --allow-anonymous
//   overwrite      an upload over the read-only share's own file
//   second-server  a second server over the same two folders
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// The cases share one folder: clear what an earlier one uploaded.
File.Delete(Path.Combine(here, "data", "up.txt"));

// Two folders of this folder, docs and data, served as shares; docs is read-only.
var options = new List<string> { "--share", "docs=docs;readonly", "--share", "data=data" };
if (which != "no-login") options.Add("--allow-anonymous");
if (which != "uploads-off") options.Add("--allow-uploads");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = options.ToArray(),
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

if (which == "overwrite")
{
    // The share's own file is not writable either: the upload is refused and the file reads as before.
    await Show("PUT docs/readme.txt", "PUT", "docs/readme.txt", "changed\n");
    await Show("GET docs/readme.txt", "GET", "docs/readme.txt");
    return 0;
}

if (which == "second-server")
{
    // A second server over the same folders: the read-only share takes no lock, the writable one does.
    foreach (string share in new[] { "docs=docs;readonly", "data=data" })
    {
        CallToolResult second = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
        {
            ["listen_urls"] = new[] { "http://127.0.0.1:0" }, ["arguments"] = new[] { "--share", share },
        });
        string name = share.Split('=')[0];
        if (second.IsError == true)
        {
            Console.WriteLine($"second server, {name}: tool error, {second.StructuredContent!.Value.GetProperty("lines")[0].GetString()}");
            continue;
        }

        string other = second.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
        CallToolResult read = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = other + "docs/readme.txt" });
        Console.WriteLine($"second server, {name}: started, GET docs/readme.txt {JsonDocument.Parse(read.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("statusCode").GetInt32()}");
    }

    return 0;
}

await Show("GET docs/readme.txt", "GET", "docs/readme.txt");
await Show("PUT docs/up.txt", "PUT", "docs/up.txt", "from the agent\n");
await Show("PUT data/up.txt", "PUT", "data/up.txt", "from the agent\n");
if (which == "no-login")
{
    return 0;
}

Console.WriteLine($"docs/up.txt on disk: {File.Exists(Path.Combine(here, "docs", "up.txt"))}");
Console.WriteLine($"data/up.txt on disk: {File.Exists(Path.Combine(here, "data", "up.txt"))}");
if (which == "solution")
{
    CallToolResult listed = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = "/docs" });
    Console.WriteLine("list_files /docs: " + string.Join(", ", listed.StructuredContent!.Value.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString())));
}
else if (which == "writable")
{
    // wait_for_event answers a change kept before the call, and waits at most 5 s for one that is not.
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "store_changed" }, ["timeout_ms"] = 5000,
    });
    JsonElement e = waited.StructuredContent!.Value.GetProperty("event");
    Console.WriteLine($"wait_for_event: {e.GetProperty("change").GetString()} {e.GetProperty("name").GetString()}");
}

return 0;

// One request through transfer; the status is what the agent reads, and a refusal is an answer, not a failure.
async Task Show(string label, string method, string path, string? body = null)
{
    var arguments = new Dictionary<string, object?> { ["url"] = url + path, ["method"] = method };
    if (body is not null) arguments["body"] = new Dictionary<string, object?> { ["content"] = body, ["isBase64"] = false };
    CallToolResult result = await curl.CallToolAsync("transfer", arguments);
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    int status = answer.GetProperty("statusCode").GetInt32();
    string text = status == 200 ? ", " + answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd() : "";
    Console.WriteLine($"{label}: {status}{text}, tool error: {(result.IsError == true ? "yes" : "no")}");
}
