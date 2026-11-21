// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side of the conversation, as a C# file-based app: `dotnet run program.cs -- SCENARIO`.
// It starts surl-mcp and curl-mcp from PATH over standard input and output with the official
// MCP SDK, calls their tools as an agent would, and prints what the recipe's claims check.
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// The scenario picks the curl options and how many files to fetch.
(string[] options, int count) = args[0] switch
{
    "capped" => (new[] { "--parallel", "--parallel-max", "4" }, 12),
    "uncapped" => (new[] { "--parallel" }, 12),
    "serial" => (Array.Empty<string>(), 3),
    "cap-of-one" => (new[] { "--parallel", "--parallel-max", "1" }, 3),
    _ => throw new ArgumentException(args[0]),
};

// surl-mcp: twelve small files in memory, every answer held 1 s by a fault rule, so transfers that
// run together overlap and the server's events can see them.
var files = new Dictionary<string, string>();
for (int i = 1; i <= 12; i++) files[$"f{i}.txt"] = $"file {i}\n";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--fault", "delay;stage=command;ms=1000" },
    ["files"] = files,
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;

// curl-mcp: the curl tool takes the command line's arguments: a glob URL, -o with #1 and --create-dirs.
if (Directory.Exists("got")) Directory.Delete("got", true);
var arguments = new List<string>(options) { "-s", "--create-dirs", "-o", "got/f#1.txt", $"{url}f[1-{count}].txt" };
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });

// What landed in the root: every file must hold the text the server holds under that name.
int fetched = Enumerable.Range(1, count).Count(i => File.Exists($"got/f{i}.txt") && File.ReadAllText($"got/f{i}.txt") == $"file {i}\n");

// surl-mcp: the server's events. An exchange is a connection: count up at a start, down at an end.
// curl has exited, but the server sees the connections close a moment later, so ask again (at most 10 s).
int open = 0, peak = 0;
for (int tries = 0; tries < 100; tries++)
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["kinds"] = new[] { "exchange_started", "exchange_ended" }, ["max_events"] = 500,
    });
    var list = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToList();
    open = 0; peak = 0;
    foreach (JsonElement e in list)
    {
        open += e.GetProperty("kind").GetString() == "exchange_started" ? 1 : -1;
        peak = Math.Max(peak, open);
    }
    if (open == 0 && list.Count > 0) break;
    await Task.Delay(100);
}
using JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
Console.WriteLine($"curl exit {answer.RootElement.GetProperty("exitCode").GetInt32()}");
Console.WriteLine($"{fetched} of {count} fetched");
Console.WriteLine($"connections {peak}");
return 0;
