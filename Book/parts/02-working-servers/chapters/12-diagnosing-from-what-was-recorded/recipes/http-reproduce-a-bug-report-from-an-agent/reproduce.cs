// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reproduces a user's bug report: it takes the user's request from get_exchanges on the
// server the user reached, replays it with the curl tool against a fresh server holding the same
// files, compares the answers, and then drops one header at a time to find the one that matters.
// Run: dotnet run reproduce.cs -- <case>
//   cut      the user's download stopped short (the default)
//   head     the same, then the replayed request head beside the reported one
//   state    the user got 404 for a file that had been deleted on that server
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "cut";
string report = "region,total\n" + string.Concat(Enumerable.Range(1, 40).Select(i => $"store-{i:D2},{1000 + i}\n"));

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

// The server the user reached, and what the user's app sent it.
(string userServer, string userUrl) = await Start();
if (which == "state")
{
    await surl.CallToolAsync("delete_file", new Dictionary<string, object?> { ["server_id"] = userServer, ["path"] = "/report.csv" });
}

string[] appHeaders = which == "state"
    ? ["-A", "shop/2.1"]
    : ["-A", "shop/2.1", "-H", "X-App-Build: 2.1.7", "-H", "Range: bytes=0-99"];
Console.WriteLine($"the user's app: {await Curl([.. appHeaders, userUrl + "report.csv"])}");

// The agent's work. 1: the user's request and the server's answer, from the record.
CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = userServer, ["scheme"] = "http" });
JsonElement exchange = got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last();
JsonElement[] chunks = [.. exchange.GetProperty("chunks").EnumerateArray()];
string head = chunks.First(c => c.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!;
string answer = string.Concat(chunks.Where(c => c.GetProperty("direction").GetString() == "sent").Select(c => c.GetProperty("data").GetString()));
string[] lines = head[..head.IndexOf("\r\n\r\n", StringComparison.Ordinal)].Split("\r\n");
string[] requestLine = lines[0].Split(' ');
List<string> headers = [.. lines.Skip(1).Where(h => !h.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))];
string reported = answer.Split("\r\n")[0].Split(' ')[1];
Console.WriteLine($"recorded: {lines[0]} with {headers.Count} headers besides Host; answered {reported}");

// 2: replay it, every header as the user sent it, against a fresh server with the same files.
(string freshServer, string freshUrl) = await Start();
string replayed = await Replay(headers);
Console.WriteLine($"replayed on a fresh server: {replayed}");
Console.WriteLine(replayed.StartsWith(reported + " ") ? $"reproduced: {reported} both times" : $"not reproduced: {reported} for the user, {replayed.Split(' ')[0]} here; the answer depends on the server's state, not the request");

if (which == "head")
{
    // The replayed request head, beside the reported one: equal but for Host.
    CallToolResult again = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = freshServer, ["scheme"] = "http" });
    string replayHead = again.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last()
        .GetProperty("chunks").EnumerateArray().First(c => c.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!;
    List<string> replayLines = [.. replayHead[..replayHead.IndexOf("\r\n\r\n", StringComparison.Ordinal)].Split("\r\n").Where(h => !h.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))];
    List<string> reportLines = [.. lines.Where(h => !h.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))];
    Console.WriteLine($"replayed head equals the reported one but for Host: {replayLines.SequenceEqual(reportLines)}");
    replayLines.ForEach(l => Console.WriteLine($"  {l}"));
    return 0;
}

// 3: if it reproduced, drop one header at a time; the ones whose absence changes the answer matter.
if (replayed.StartsWith(reported + " "))
{
    foreach (string header in headers)
    {
        string without = await Replay([.. headers.Where(h => h != header)]);
        if (!without.StartsWith(reported + " "))
        {
            Console.WriteLine($"without {header}: {without}; the answer turns on it");
        }
    }

    return 0;
}

return 1;

// A server on a free port holding the report, as the user's server did.
async Task<(string Id, string Url)> Start()
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "http://127.0.0.1:0" },
        ["files"] = new Dictionary<string, string> { ["report.csv"] = report },
    });
    JsonElement s = started.StructuredContent!.Value;
    return (s.GetProperty("server_id").GetString()!, s.GetProperty("bound_urls")[0].GetString()!);
}

// The request again: the method, the target and each header but Host, with -H, on the fresh server.
Task<string> Replay(List<string> with)
{
    List<string> argv = requestLine[0] == "GET" ? [] : ["-X", requestLine[0]];
    foreach (string h in with)
    {
        argv.AddRange(["-H", h]);
    }

    argv.Add(freshUrl.TrimEnd('/') + requestLine[1]);
    return Curl([.. argv]);
}

// The curl tool: the status and the bytes received.
async Task<string> Curl(string[] argv)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "--max-time", "10", "-o", "body.out", "-w", "%{http_code} %{size_download} bytes" }.Concat(argv).ToArray(),
    });
    using JsonDocument doc = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    return doc.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();
}
