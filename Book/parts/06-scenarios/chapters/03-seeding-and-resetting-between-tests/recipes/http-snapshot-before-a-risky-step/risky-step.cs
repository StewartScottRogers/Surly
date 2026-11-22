// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent snapshots a surl-mcp server before a step that may wreck its files, runs the step
// through curl-mcp, and restores the snapshot when the step fails.
// Run: dotnet run risky-step.cs [-- fails|five-snapshots|other-server|on-disk]
//   fails           snapshot, a step that half-succeeds then fails, restore (the default)
//   five-snapshots  take five snapshots, then restore the first
//   other-server    restore the snapshot into a second server
//   on-disk         snapshot a server whose files are on disk
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "fails";

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

// The test data: two files in memory, served over HTTP and FTP, open to anonymous uploads;
// on-disk serves the folder data/ instead.
var start = new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "ftp://127.0.0.1:0" },
    ["arguments"] = mode == "on-disk"
        ? new[] { "--directory", "data", "--allow-anonymous", "--allow-uploads" }
        : new[] { "--allow-anonymous", "--allow-uploads" },
};
if (mode != "on-disk")
{
    start["files"] = new Dictionary<string, string> { ["users.json"] = "[{\"name\":\"alice\"}]\n", ["orders/1.json"] = "{\"id\":1}\n" };
}

JsonElement server = (await surl.CallToolAsync("start_server", start)).StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string http = server.GetProperty("bound_urls")[0].GetString()!, ftp = server.GetProperty("bound_urls")[1].GetString()!;

// Before the risky step: a snapshot.
CallToolResult snapshot = await surl.CallToolAsync("snapshot_server", new Dictionary<string, object?> { ["server_id"] = id });
if (snapshot.IsError == true)
{
    Console.WriteLine($"snapshot_server refused: {Text(snapshot)}");
    return 1;
}

string saved = snapshot.StructuredContent!.Value.GetProperty("snapshot_id").GetString()!;
Console.WriteLine($"snapshot: {saved}");

switch (mode)
{
    case "five-snapshots":
        for (int i = 0; i < 4; i++)
        {
            await surl.CallToolAsync("snapshot_server", new Dictionary<string, object?> { ["server_id"] = id });
        }

        return await Restore(id, saved);

    case "other-server":
        JsonElement other = (await surl.CallToolAsync("start_server", new Dictionary<string, object?> { ["listen_urls"] = new[] { "http://127.0.0.1:0" } })).StructuredContent!.Value;
        return await Restore(other.GetProperty("server_id").GetString()!, saved);
}

// The risky step: overwrite users.json, then archive it into a folder that is not there.
int written = await Curl("-s", "-T", "broken.json", ftp + "users.json");
int archived = await Curl("-s", "-T", "broken.json", ftp + "archive/users.json");
Console.WriteLine($"step: overwrite users.json, curl exit {written}; archive it, curl exit {archived}");
Console.WriteLine($"after the step: users.json is {await Get("users.json")}");

// The step failed, so put the data back as it was before it.
if (archived != 0)
{
    int restored = await Restore(id, saved);
    if (restored != 0)
    {
        return restored;
    }
}

Console.WriteLine($"after the restore: users.json is {await Get("users.json")}");

// What the server recorded: each change to the content store, in order.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "content",
});
foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"recorded: {e.GetProperty("change")} {e.GetProperty("name")}");
}

return 0;

// restore_server: puts the snapshot back, or says why it cannot.
async Task<int> Restore(string serverId, string snapshotId)
{
    CallToolResult result = await surl.CallToolAsync("restore_server", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["snapshot_id"] = snapshotId,
    });
    Console.WriteLine(result.IsError == true ? $"restore_server refused: {Text(result)}" : $"restored: {snapshotId}");
    return result.IsError == true ? 1 : 0;
}

// curl-mcp's curl tool: curl's exit code.
async Task<int> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    using JsonDocument answer = JsonDocument.Parse(Text(result));
    return answer.RootElement.GetProperty("exitCode").GetInt32();
}

// curl-mcp's transfer: one GET over HTTP, the body as text.
async Task<string> Get(string path)
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = http + path });
    using JsonDocument answer = JsonDocument.Parse(Text(fetched));
    return answer.RootElement.GetProperty("body").GetProperty("content").GetString()!.TrimEnd();
}

static string Text(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;
