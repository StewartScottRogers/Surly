// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches five files at once over HTTP/2 through curl-mcp's curl tool, from a surl-mcp
// server, and asks the server how many connections it accepted.
// Run: dotnet run multiplex.cs [-- slow | http1.1]
//   (no argument) curl -Z with --http2-prior-knowledge
//   slow          the server holds every request 2 s; times the five with -Z and without it
//   http1.1       curl -Z with --http1.1, which cannot share a connection
#:package ModelContextProtocol.Core@2.4.0

using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "";

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

// surl-mcp: five small files in memory; with slow, every request is held 2 s.
var files = new Dictionary<string, string>();
for (int i = 1; i <= 5; i++)
{
    files[$"f{i}.txt"] = $"file {i}\n";
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = files,
    ["arguments"] = mode == "slow" ? new[] { "--fault", "delay;stage=command;ms=2000" } : Array.Empty<string>(),
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string urls = server.GetProperty("bound_urls")[0].GetString()! + "f[1-5].txt";

if (mode == "slow")
{
    foreach (bool parallel in new[] { true, false })
    {
        var clock = Stopwatch.StartNew();
        await Fetch(parallel ? ["--http2-prior-knowledge", "-Z"] : ["--http2-prior-knowledge"]);
        Console.WriteLine($"{(parallel ? "with -Z" : "without -Z")}: {(int)clock.Elapsed.TotalSeconds} s");
    }

    Console.WriteLine($"connections: {await Connections()}");
    return 0;
}

// curl-mcp's curl tool: one command line, five URLs from the glob; -w prints one line per transfer.
JsonElement answer = await Fetch([mode == "http1.1" ? "--http1.1" : "--http2-prior-knowledge", "-Z", "-w", "%{http_version} %{num_connects}\n"]);
string[] lines = answer.GetProperty("standardOutput").GetProperty("content").GetString()!
    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
Console.WriteLine($"versions: {string.Join(" ", lines.Select(l => l.Split(' ')[0]).Distinct())}");
Console.WriteLine($"transfers: {lines.Length}, that opened a connection: {lines.Count(l => l.EndsWith(" 1"))}");
Console.WriteLine($"connections: {await Connections()}");
return 0;

async Task<JsonElement> Fetch(string[] options)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = options.Concat(new[] { "-s", "-o", "got/f#1.txt", "--create-dirs", urls }).ToArray(),
    });
    return JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// surl-mcp's list_servers: exchange_count is the connections the server has accepted.
async Task<int> Connections()
{
    CallToolResult listed = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
    return listed.StructuredContent!.Value.GetProperty("servers").EnumerateArray()
        .Single(s => s.GetProperty("server_id").GetString() == serverId).GetProperty("exchange_count").GetInt32();
}
