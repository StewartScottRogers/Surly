// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a backup tool's upload against a full disk: surl-mcp serves FTP with /backups out of
// space, curl-mcp's curl tool uploads db.dump from the folder, and list_files and get_events check what
// the server kept and refused. Then /staging fills while the server runs, and is freed again.
// Run: dotnet run program.cs -- <case>
//   full       the out-of-space server (the default)
//   append     append to last night's dump in /backups
//   subfolder  upload into /backups/2026, a folder made on the way
//   old        upload into /backups-old, beside /backups
//   closed     a server that takes no uploads at all
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "full";

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

// surl-mcp: anonymous FTP in memory, last night's dump in /backups; uploads on and /backups full,
// or, for "closed", no uploads at all.
string[] arguments = which == "closed"
    ? ["--allow-anonymous"]
    : ["--allow-anonymous", "--allow-uploads", "--fault", "out-of-space;path=/backups"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["backups/2026-11-19.dump"] = "pg_dump of 2026-11-19\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string root = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "append":
        await Upload("backups/2026-11-19.dump", "--append");
        CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/backups/2026-11-19.dump" });
        Console.WriteLine($"2026-11-19.dump: {read.StructuredContent!.Value.GetProperty("text").GetString()!.Trim()}");
        return 0;
    case "subfolder":
        await Upload("backups/2026/2026-11-20.dump");
        await List("/backups");
        await List("/backups/2026");
        return 0;
    case "old":
        await Upload("backups-old/2026-11-20.dump");
        await List("/backups-old");
        return 0;
}

await Upload("backups/2026-11-20.dump");
await List("/backups");
if (which == "closed")
{
    return 0;
}

await Upload("staging/a.dump");

// The disk under /staging fills while the server runs, then is freed.
CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault"] = "out-of-space;path=/staging" });
string fault = added.StructuredContent!.Value.GetProperty("fault_id").GetString()!;
Console.WriteLine($"add_fault: {fault}");
await Upload("staging/b.dump");
await surl.CallToolAsync("remove_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault_id"] = fault });
Console.WriteLine($"remove_fault: {fault}");
await Upload("staging/b.dump");
await List("/staging");

// surl-mcp: every time a rule acted, and on what.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "fault_applied" } });
foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"fault_applied: {e.GetProperty("rule_id").GetString()} {e.GetProperty("fault_kind").GetString()} {e.GetProperty("target").GetString()}");
}

return 0;

// curl-mcp: the backup tool's upload of db.dump, with any folder made on the way.
async Task Upload(string target, params string[] extra)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = (string[])["-sS", "--ftp-create-dirs", .. extra, "-T", "db.dump", root + target],
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string errors = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.Replace("\r", "").Trim();
    Console.WriteLine($"upload {target}: exitCode {answer.RootElement.GetProperty("exitCode")}, tool error {(result.IsError == true ? "yes" : "no")}{(errors.Length > 0 ? ", " + errors : "")}");
}

// surl-mcp: a folder of the server's store, without a request.
async Task List(string folder)
{
    CallToolResult listed = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = folder });
    var names = listed.StructuredContent!.Value.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString() + (e.GetProperty("kind").GetString() == "folder" ? "/" : ""));
    Console.WriteLine($"{folder}: {string.Join(", ", names)}");
}
