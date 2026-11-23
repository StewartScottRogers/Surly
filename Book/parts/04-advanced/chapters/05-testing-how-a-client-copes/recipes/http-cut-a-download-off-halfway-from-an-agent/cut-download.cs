// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a server that cuts every transfer after 4,000,000 bytes through surl-mcp, downloads a
// large file with curl-mcp, gets cut off, and resumes.
// Run: dotnet run cut-download.cs -- <case>
//   resume   download with the curl tool, get cut off, resume with -C - (the default)
//   restart  the curl tool retrying from the start, as a client that does not resume would
//   short    transfer of a short file through the same rule
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "resume";

// big.txt, in a folder the server will serve: 5,000,000 random hexadecimal digits and a line end.
string here = Directory.GetCurrentDirectory();
Directory.CreateDirectory("site");
Directory.CreateDirectory("dl");
byte[] big = System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(2_500_000)) + "\n");
File.WriteAllBytes(Path.Combine("site", "big.txt"), big);
File.WriteAllText(Path.Combine("site", "hello.txt"), "hello from surl\n");
File.Delete(Path.Combine("dl", "big.txt"));

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
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

// The server serves site/ and drops every connection once it has sent 4,000,000 bytes on it.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--directory", "site", "--fault", "drop;after-bytes=4000000" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!;

if (which == "short")
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt", ["timeoutSeconds"] = 30 });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"transfer: statusCode {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}");
    return 0;
}

// The curl tool runs curl's own words and writes the download under CURL_MCP_ROOT.
async Task<int> Curl(string label, params string[] words)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    int exit = run.GetProperty("exitCode").GetInt32();
    Console.WriteLine($"{label}: exitCode {exit}, {run.GetProperty("standardOutput").GetProperty("content").GetString()}");
    return exit;
}

if (which == "restart")
{
    await Curl("restarting", "-sS", "--max-time", "30", "--retry", "2", "--retry-all-errors", "--retry-delay", "1",
        "-o", "dl/again.txt", "-w", "%{num_retries} retries", url + "big.txt");
    return 0;
}

int first = await Curl("from the start", "-sS", "--max-time", "30", "-o", "dl/big.txt", "-w", "%{http_code} %{size_download}", url + "big.txt");
if (first != 0)
{
    Console.WriteLine($"partial file: {new FileInfo(Path.Combine("dl", "big.txt")).Length} bytes");
    await Curl("resumed", "-sS", "--max-time", "30", "-C", "-", "-o", "dl/big.txt", "-w", "%{http_code}", url + "big.txt");
}
Console.WriteLine($"whole file: {(File.ReadAllBytes(Path.Combine("dl", "big.txt")).AsSpan().SequenceEqual(big) ? "yes" : "no")}");

// surl-mcp get_events: the server's own account of the cut.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "fault_applied" },
});
foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
    Console.WriteLine($"fault_applied: rule {e.GetProperty("rule_id")}, kind {e.GetProperty("fault_kind")}");
return 0;
