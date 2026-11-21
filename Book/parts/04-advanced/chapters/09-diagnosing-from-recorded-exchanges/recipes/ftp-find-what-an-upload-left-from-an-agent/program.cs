// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An upload job runs over FTP and exits 0; the agent then lists and reads what the server's store holds
// and checks it against what the job meant to upload. Run: dotnet run program.cs [-- <case>]
//   job         the job uploads three files with one curl call and no --fail-early (the default)
//   fail-early  the same job with --fail-early
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "job";

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

// The server: anonymous FTP that takes uploads (both loosen surl's defaults), with a reports folder.
JsonElement server = (await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous", "--allow-uploads" },
    ["files"] = new Dictionary<string, string> { ["reports/README.txt"] = "uploads land here\n" },
})).StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!;

// The job: what it means to upload, local file to remote path. One target folder does not exist.
var plan = new (string Local, string Remote)[]
{
    ("out/daily.csv", "/reports/daily.csv"),
    ("out/weekly.csv", "/archive/weekly.csv"),
    ("out/monthly.csv", "/reports/monthly.csv"),
};
var jobArgs = new List<string> { "-s", "--max-time", "20", "-w", "%{url.path} %{exitcode}\\n" };
if (which == "fail-early") jobArgs.Add("--fail-early");
foreach ((string local, string remote) in plan) jobArgs.AddRange(["-T", local, url.TrimEnd('/') + remote]);
JsonElement job = JsonDocument.Parse((await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = jobArgs }))
    .Content.OfType<TextContentBlock>().Single().Text).RootElement;
string perFile = job.GetProperty("standardOutput").GetProperty("content").GetString()!;
Console.WriteLine($"job: curl exit {job.GetProperty("exitCode")}; per file: {string.Join(" | ", perFile.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))}");

// The agent's part, from the server's store alone. list_files: what each folder holds.
foreach (string folder in plan.Select(p => p.Remote[..p.Remote.LastIndexOf('/')]).Distinct())
{
    CallToolResult listed = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = folder });
    if (listed.IsError == true)
    {
        Console.WriteLine($"{folder}: {listed.Content.OfType<TextContentBlock>().Single().Text}");
        continue;
    }

    IEnumerable<string> entries = listed.StructuredContent!.Value.GetProperty("entries").EnumerateArray()
        .Select(e => $"{e.GetProperty("name")} ({e.GetProperty("byte_count")} bytes)");
    Console.WriteLine($"{folder}: {string.Join(", ", entries)}");
}

// read_file: each planned file, compared with the local bytes the job sent.
int missing = 0;
foreach ((string local, string remote) in plan)
{
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = remote });
    if (read.IsError == true)
    {
        missing++;
        Console.WriteLine($"{remote}: missing ({read.Content.OfType<TextContentBlock>().Single().Text})");
        continue;
    }

    string stored = read.StructuredContent!.Value.GetProperty("text").GetString()!;
    Console.WriteLine($"{remote}: {(stored == File.ReadAllText(local) ? "same as " + local : "differs from " + local)}");
}

// get_events: the store's own account of what was written.
JsonElement changes = (await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id,
    ["kinds"] = new[] { "store_changed" },
})).StructuredContent!.Value.GetProperty("events");
IEnumerable<string> written = changes.EnumerateArray().Where(e => e.GetProperty("change").GetString() == "written").Select(e => e.GetProperty("name").GetString()!);
Console.WriteLine($"written: {string.Join(", ", written)}");
Console.WriteLine($"verdict: {(missing == 0 ? "everything landed" : $"{missing} of {plan.Length} files did not land; the job exited {job.GetProperty("exitCode")}")}");
return missing == 0 ? 0 : 1;
