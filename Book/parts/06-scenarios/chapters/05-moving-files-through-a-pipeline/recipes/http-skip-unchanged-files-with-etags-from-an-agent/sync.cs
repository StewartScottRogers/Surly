// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a sync job: curl-mcp's curl tool fetches each file from a surl-mcp server only when
// the server's copy is newer than the local one, and surl-mcp changes the server between runs.
// Run: dotnet run sync.cs -- <case>
//   changed  run, run again, rewrite a.txt with put_file, run again (the default)
//   deleted  run, delete a.txt with delete_file, run again
//   no-fail  the deleted case, the job's curl lines without -f
//   dates    run once, then compare the server's Last-Modified with the saved file's date
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "changed";

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

// surl-mcp: a server holding two files in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["a.txt"] = "alpha\n", ["b.txt"] = "beta\n" },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string folder = $"local-{which}";
bool fail = which != "no-fail";

await Sync(1);
if (which == "dates")
{
    // transfer: a HEAD request, answered as JSON text with the headers as name and value pairs.
    CallToolResult head = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "a.txt", ["method"] = "HEAD" });
    using JsonDocument headAnswer = JsonDocument.Parse(head.Content.OfType<TextContentBlock>().Single().Text);
    string lastModified = headAnswer.RootElement.GetProperty("headers").EnumerateArray()
        .First(h => h.GetProperty("name").GetString() == "Last-Modified").GetProperty("value").GetString()!;
    DateTime saved = File.GetLastWriteTimeUtc(Path.Combine(here, folder, "a.txt"));
    Console.WriteLine($"HEAD a.txt: {headAnswer.RootElement.GetProperty("statusCode")}");
    Console.WriteLine($"saved a.txt dated as Last-Modified: {(saved == DateTime.Parse(lastModified).ToUniversalTime() ? "yes" : "no")}");

    // The condition curl sends: -v writes the request head to standard error.
    CallToolResult verbose = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-v", "-z", $"{folder}/a.txt", "-o", $"{folder}/a.txt", url + "a.txt" },
    });
    using JsonDocument verboseAnswer = JsonDocument.Parse(verbose.Content.OfType<TextContentBlock>().Single().Text);
    string sent = verboseAnswer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!;
    Console.WriteLine($"curl sent If-Modified-Since with that date: {(sent.Contains($"> If-Modified-Since: {lastModified}") ? "yes" : "no")}");
    return 0;
}

await Sync(2);
if (which is "deleted" or "no-fail")
{
    await surl.CallToolAsync("delete_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = "/a.txt" });
    Console.WriteLine("a.txt deleted");
}
else
{
    // Last-Modified counts whole seconds: wait past the second the files were served in.
    await Task.Delay(TimeSpan.FromSeconds(1.1));
    await surl.CallToolAsync("put_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = "/a.txt", ["text"] = "alpha, revised\n" });
    Console.WriteLine("a.txt rewritten");
}

await Sync(3);
Console.WriteLine($"local a.txt: {File.ReadAllText(Path.Combine(here, folder, "a.txt")).TrimEnd()}");
return 0;

// One run of the job: one curl tool call per file, -z comparing with the local copy's date.
async Task Sync(int run)
{
    var report = new List<string>();
    foreach (string name in new[] { "a.txt", "b.txt" })
    {
        string[] line = [fail ? "-f" : "-s", "-s", "-R", "-z", $"{folder}/{name}", "--create-dirs", "-o", $"{folder}/{name}", "-w", "%{http_code}", url + name];
        CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = line });
        using JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
        string code = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();
        report.Add($"{name} {code} (exit {answer.RootElement.GetProperty("exitCode")})");
    }

    Console.WriteLine($"run {run}: {string.Join(", ", report)}");
}
