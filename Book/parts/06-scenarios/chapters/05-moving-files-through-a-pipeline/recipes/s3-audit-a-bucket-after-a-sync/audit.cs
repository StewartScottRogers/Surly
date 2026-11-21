// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent audits an S3 bucket after a sync. The sync is one curl-mcp call that puts every file of out/ into
// the bucket; the audit lists the bucket's store with surl-mcp's list_files, reads what it needs with read_file
// and reports the objects that are missing, extra or changed against the manifest, which is the out/ folder.
// server.pem and server.key are in the folder.
// Run: dotnet run audit.cs [-- drift|clean|partial|client-view|no-listing]
//   (no argument) sync, then audit
//   drift         sync, then change one object, delete another and add a third, then audit
//   clean         sync, remove the stale object left from the last run, then audit
//   partial       a sync that names a file out/ does not have
//   client-view   compare the store's listing with the listing a client gets over S3
//   no-listing    a server started without --list-directories
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "sync";

// The source of the sync, and so the manifest: three files, name to text.
Directory.CreateDirectory("out");
Dictionary<string, string> manifest = new()
{
    ["a.csv"] = "region,total\nnorth,1200\n", ["b.csv"] = "region,total\nsouth,950\n", ["c.csv"] = "region,total\neast,700\n",
};
foreach ((string file, string content) in manifest) File.WriteAllText($"out/{file}", content);

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

// surl-mcp: an S3 server in memory, with the bucket reports holding an object a past run left behind.
var surlArguments = new List<string>
{
    "--cert", "server.pem", "--key", "server.key", "--user", "AKIAIOSFODNN7EXAMPLE:s3cret", "--allow-uploads",
};
if (which != "no-listing") surlArguments.Add("--list-directories");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["reports/old.csv"] = "region,total\nwest,1\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int port = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;

// The sync: one curl call, a put for each file it is given.
string[] names = which == "partial" ? ["a.csv", "gone.csv", "b.csv", "c.csv"] : ["a.csv", "b.csv", "c.csv"];
List<string> sync = ["-sS", .. S3Options()];
foreach (string name in names) sync.AddRange(["-T", $"out/{name}", $"s3://reports/{name}"]);
var (exit, error, said) = await Curl(sync);
// Of curl's complaints, the last line, which carries the error code.
if (said.Contains("curl: (")) said = said[said.LastIndexOf("curl: (")..];
Console.WriteLine($"sync: exit {exit}, tool error: {(error ? "yes" : "no")}{(said.Length > 0 ? ", " + said : "")}");

switch (which)
{
    case "drift":
        // Someone changes an object to text of the same length, deletes another and leaves a third.
        await Tool("put_file", new() { ["server_id"] = serverId, ["path"] = "/reports/a.csv", ["text"] = "region,total\nnorth,9999\n" });
        await Tool("delete_file", new() { ["server_id"] = serverId, ["path"] = "/reports/b.csv" });
        await Tool("put_file", new() { ["server_id"] = serverId, ["path"] = "/reports/z.csv", ["text"] = "extra\n" });
        break;
    case "clean":
        await Tool("delete_file", new() { ["server_id"] = serverId, ["path"] = "/reports/old.csv" });
        break;
    case "client-view":
        var (_, _, listing) = await Curl(["-s", .. S3Options(), "s3://reports/"]);
        Console.WriteLine($"client listing: {listing}");
        break;
    case "no-listing":
        var (denied, deniedError, deniedText) = await Curl(["-sS", .. S3Options(), "s3://reports/"]);
        Console.WriteLine($"client listing: exit {denied}, tool error: {(deniedError ? "yes" : "no")}, {deniedText}");
        break;
}

// The audit: the store's listing against the manifest.
JsonElement entries = (await Tool("list_files", new() { ["server_id"] = serverId, ["path"] = "/reports" })).GetProperty("entries");
Dictionary<string, long> stored = entries.EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString()!, e => e.GetProperty("byte_count").GetInt64());
Console.WriteLine($"store: {string.Join(" ", stored.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}({p.Value})"))}");

List<string> findings = [];
foreach ((string name, string expected) in manifest)
{
    if (!stored.TryGetValue(name, out long size)) findings.Add($"missing {name}");
    else if (size != expected.Length) findings.Add($"changed {name}: {size} bytes, expected {expected.Length}");
    else if ((await Tool("read_file", new() { ["server_id"] = serverId, ["path"] = $"/reports/{name}" })).GetProperty("text").GetString() != expected)
        findings.Add($"changed {name}: same size, different bytes");
}

foreach (string name in stored.Keys.Where(n => !manifest.ContainsKey(n)).Order(StringComparer.Ordinal)) findings.Add($"extra {name}");
Console.WriteLine(findings.Count == 0 ? $"audit: bucket matches the manifest, {manifest.Count} objects" : $"audit: {string.Join("; ", findings)}");
return 0;

// The options every S3 request from curl needs: the certificate, the signing login, and a route to the server.
string[] S3Options() => ["--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{port}", "-u", "AKIAIOSFODNN7EXAMPLE:s3cret"];

// One curl-mcp call: the exit code, whether the tool called it an error, and standard output and error together.
async Task<(int Exit, bool IsError, string Text)> Curl(List<string> argv)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string Stream(string name) => (answer.RootElement.GetProperty(name).GetProperty("content").GetString() ?? "").Replace("\r", "").Trim();
    return (answer.RootElement.GetProperty("exitCode").GetInt32(), result.IsError == true,
        string.Join(" | ", new[] { Stream("standardOutput"), Stream("standardError") }.Where(s => s.Length > 0)).Replace("\n", " "));
}

// One surl-mcp call, answered as its structured content.
async Task<JsonElement> Tool(string tool, Dictionary<string, object?> arguments)
{
    CallToolResult result = await surl.CallToolAsync(tool, arguments);
    return result.StructuredContent!.Value;
}
