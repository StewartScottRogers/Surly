// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent uploads three files in one curl-mcp call and checks them with surl-mcp's list_files.
// Run: dotnet run program.cs -- <case>
//   upload          -T a.txt, b.txt and c.txt to /reports/, logged in, then list_files /reports (the default)
//   no-login        the same call with no login
//   missing-file    the middle file is not in the folder
//   outside-root    the middle file is outside CURL_MCP_ROOT
//   glob            one -T "{a,b,c}.txt", as on the console
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "upload";

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

// surl-mcp: an account, uploads on, and a report already in /reports, in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "alice:s3cret", "--allow-uploads" },
    ["files"] = new Dictionary<string, string> { ["reports/old.txt"] = "from last week\n" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp: one call, one curl command line, an -T and a URL for each file. No shell is involved.
string[] files = which switch
{
    "missing-file" => ["a.txt", "missing.txt", "c.txt"],
    "outside-root" => ["a.txt", "../b.txt", "c.txt"],
    "glob" => ["{a,b,c}.txt"],
    _ => ["a.txt", "b.txt", "c.txt"],
};
var argv = new List<string> { "-s", "-w", "%{http_code} %{url.path}\n" };
if (which != "no-login")
{
    argv.AddRange(["--digest", "-u", "alice:s3cret"]);
}

foreach (string file in files)
{
    argv.AddRange(["-T", file, url + "reports/"]);
}

CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
JsonElement root = answer.RootElement;
Console.WriteLine($"exitCode: {root.GetProperty("exitCode").GetRawText()}");
if (root.GetProperty("standardOutput").ValueKind == JsonValueKind.Object)
{
    Console.Write(root.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n"));
}

if (root.GetProperty("failure").ValueKind == JsonValueKind.Object)
{
    Console.WriteLine($"failure: {root.GetProperty("failure").GetProperty("text").GetString()!.Split('\n')[0].TrimEnd('\r')}");
}

Console.WriteLine($"curl tool error: {(sent.IsError == true ? "yes" : "no")}");

// surl-mcp: what the server's store holds in /reports now, read without a request.
CallToolResult listed = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/reports" });
var names = listed.StructuredContent!.Value.GetProperty("entries").EnumerateArray()
    .Select(e => $"{e.GetProperty("name").GetString()} ({e.GetProperty("byte_count")} bytes)");
Console.WriteLine($"/reports: {string.Join(", ", names)}");
return sent.IsError == true ? 1 : 0;
