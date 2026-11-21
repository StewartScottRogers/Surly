// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent snapshots, restores and resets a running surl server's files through surl-mcp, and
// fetches with curl through curl-mcp to see what is served. Run: dotnet run reset.cs -- <case>
//   solution                 snapshot after set-up, change things, restore the snapshot
//   reset-to-start           reset_server after set-up
//   snapshot-outlives-reset  restore the snapshot after a reset
//   exchanges-stay           list_servers after restores, a reset and four requests
//   newest-four              four more snapshots, then restore the first
//   not-on-disk              reset and snapshot a second server, whose files are a folder on disk
#:package ModelContextProtocol.Core@*

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

// A plain HTTP server on a free port, holding one file in memory, and a test's set-up: fixture.txt.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
await surl.CallToolAsync("put_file", new Dictionary<string, object?>
{
    ["server_id"] = server, ["path"] = "/fixture.txt", ["text"] = "set up once\n",
});

// snapshot_server copies the server's stores and answers the snapshot's ID.
CallToolResult snapshotted = await surl.CallToolAsync("snapshot_server", new Dictionary<string, object?> { ["server_id"] = server });
string snapshot = snapshotted.StructuredContent!.Value.GetProperty("snapshot_id").GetString()!;

switch (which)
{
    case "solution":
        await LeaveSomethingBehind();
        await Restore();
        await Fetch("scratch.txt");
        await Fetch("hello.txt");
        break;
    case "reset-to-start":
        await Reset();
        await Fetch("fixture.txt");
        await Fetch("hello.txt");
        break;
    case "snapshot-outlives-reset":
        await Reset();
        await Restore();
        CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["path"] = "/fixture.txt",
        });
        Console.WriteLine($"read_file /fixture.txt: {read.StructuredContent!.Value.GetProperty("text").GetRawText()}");
        break;
    case "exchanges-stay":
        await LeaveSomethingBehind();
        await Restore();
        await Fetch("scratch.txt");
        await Fetch("hello.txt");
        await Reset();
        await Fetch("fixture.txt");
        await Fetch("hello.txt");
        await Restore();
        CallToolResult servers = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
        JsonElement mine = servers.StructuredContent!.Value.GetProperty("servers").EnumerateArray()
            .Single(s => s.GetProperty("server_id").GetString() == server);
        Console.WriteLine($"list_servers: exchange_count {mine.GetProperty("exchange_count")}");
        break;
    case "newest-four":
        for (int i = 0; i < 4; i++)
        {
            await surl.CallToolAsync("snapshot_server", new Dictionary<string, object?> { ["server_id"] = server });
        }

        await Restore();
        break;
    case "not-on-disk":
        // A second server: --directory serves the folder site, on disk, instead of files in memory.
        CallToolResult onDisk = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
        {
            ["listen_urls"] = new[] { "http://127.0.0.1:0" },
            ["arguments"] = new[] { "--directory", "site" },
        });
        string disk = onDisk.StructuredContent!.Value.GetProperty("server_id").GetString()!;
        Report("reset_server", await surl.CallToolAsync("reset_server", new Dictionary<string, object?> { ["server_id"] = disk }));
        Report("snapshot_server", await surl.CallToolAsync("snapshot_server", new Dictionary<string, object?> { ["server_id"] = disk }));
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// What a test might leave behind: a file added and a file deleted.
async Task LeaveSomethingBehind()
{
    await surl.CallToolAsync("put_file", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["path"] = "/scratch.txt", ["text"] = "left by a test\n",
    });
    await surl.CallToolAsync("delete_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = "/hello.txt" });
}

// restore_server and reset_server answer the IDs they were given, or are a tool error that says why not.
async Task Restore() => Report("restore_server", await surl.CallToolAsync("restore_server", new Dictionary<string, object?>
{
    ["server_id"] = server, ["snapshot_id"] = snapshot,
}));

async Task Reset() => Report("reset_server", await surl.CallToolAsync("reset_server", new Dictionary<string, object?> { ["server_id"] = server }));

void Report(string call, CallToolResult result)
{
    if (result.IsError == true)
    {
        Console.WriteLine($"{call}: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
        return;
    }

    JsonElement answer = result.StructuredContent!.Value;
    bool sameServer = answer.GetProperty("server_id").GetString() == server;
    bool sameSnapshot = !answer.TryGetProperty("snapshot_id", out JsonElement id) || id.GetString() == snapshot;
    Console.WriteLine(sameServer && sameSnapshot ? $"{call}: answered the IDs it was given" : $"{call}: {answer.GetRawText()}");
}

// transfer is curl-mcp's plain fetch; it answers the status and the body, in text, as JSON.
async Task Fetch(string path)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int status = answer.RootElement.GetProperty("statusCode").GetInt32();
    string body = status == 200 ? " " + answer.RootElement.GetProperty("body").GetProperty("content").GetString()!.TrimEnd('\n') : "";
    Console.WriteLine($"transfer {path}: {status}{body}");
}
