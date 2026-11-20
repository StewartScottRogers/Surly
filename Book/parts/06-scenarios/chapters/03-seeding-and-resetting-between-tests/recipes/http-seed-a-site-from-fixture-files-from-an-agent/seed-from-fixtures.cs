// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds a surl-mcp server's memory from the fixtures folder, lets a test change a file
// over FTP through curl-mcp, reads the change over HTTP, and resets the server to the originals.
// Run: dotnet run seed-from-fixtures.cs [-- test|strict|no-uploads|on-disk]
//   test        seed, upload over FTP, read over HTTP, check the disk, reset (the default)
//   strict      the same server with --allow-anonymous and --allow-uploads left out
//   no-uploads  --allow-anonymous given, --allow-uploads left out
//   on-disk     serve the fixtures folder itself with --directory, then ask for a reset
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "test";

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

// Every file under fixtures/, by its '/' separated path, as text for start_server's files.
var files = Directory.EnumerateFiles("fixtures", "*", SearchOption.AllDirectories).Order()
    .ToDictionary(f => Path.GetRelativePath("fixtures", f).Replace('\\', '/'), File.ReadAllText);
string[] loosen = mode switch
{
    "strict" => [],
    "no-uploads" => ["--allow-anonymous"],
    "on-disk" => ["--directory", "fixtures", "--allow-anonymous", "--allow-uploads"],
    _ => ["--allow-anonymous", "--allow-uploads"],
};
var start = new Dictionary<string, object?> { ["listen_urls"] = new[] { "http://127.0.0.1:0", "ftp://127.0.0.1:0" }, ["arguments"] = loosen };
if (mode != "on-disk")
{
    start["files"] = files;
}

CallToolResult started = await surl.CallToolAsync("start_server", start);
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string http = server.GetProperty("bound_urls")[0].GetString()!, ftp = server.GetProperty("bound_urls")[1].GetString()!;
Console.WriteLine($"seeded: {string.Join(", ", mode == "on-disk" ? ["(the folder itself)"] : files.Keys)}");

// The test: replace users.json over FTP with curl -T, then read it over HTTP.
CallToolResult upload = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", "-T", "new-users.json", ftp + "users.json" } });
using (JsonDocument answer = JsonDocument.Parse(upload.Content.OfType<TextContentBlock>().Single().Text))
{
    Console.WriteLine($"FTP upload users.json: curl exit {answer.RootElement.GetProperty("exitCode")}");
}

Console.WriteLine($"HTTP GET users.json: {await Get("users.json")}");
Console.WriteLine($"fixtures/users.json on disk: {File.ReadAllText("fixtures/users.json").TrimEnd()}");

// The next test starts from the originals: reset_server returns the store to its state at start.
CallToolResult reset = await surl.CallToolAsync("reset_server", new Dictionary<string, object?> { ["server_id"] = id });
if (reset.IsError == true)
{
    Console.WriteLine($"reset_server refused: {reset.Content.OfType<TextContentBlock>().Single().Text}");
    return 1;
}

Console.WriteLine($"after reset, HTTP GET users.json: {await Get("users.json")}");
return 0;

// curl-mcp's transfer: one GET over HTTP, the body as text.
async Task<string> Get(string path)
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = http + path });
    using JsonDocument answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("body").GetProperty("content").GetString()!.TrimEnd();
}
