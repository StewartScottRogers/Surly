// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches small files from a surl-mcp server and reads back, with get_exchanges, which
// connection carried each request.
// Run: dotnet run reuse.cs -- <case>
//   glob        one curl tool call, a URL glob naming a.txt, b.txt and c.txt (the default)
//   urls        one curl tool call naming the three URLs one by one
//   two-calls   two curl tool calls, a.txt then b.txt
//   close       the glob call, each request sending Connection: close
//   http1.0     one curl tool call for a.txt and b.txt over HTTP/1.0
//   rate        one curl tool call for a.txt and b.txt, 2 s apart (--rate 1/2s)
//   idle        the same, against a server started with --idle-timeout 1
//   transfers   three transfer tool calls, one file each
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "glob";

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

// surl-mcp: three small files in memory, on a free port; a 1 s idle timeout when the case asks.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["a.txt"] = "file a\n", ["b.txt"] = "file b\n", ["c.txt"] = "file c\n" },
    ["arguments"] = which == "idle" ? new[] { "--idle-timeout", "1" } : Array.Empty<string>(),
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!;

if (which == "transfers")
{
    // transfer: one request per call, described by fields.
    foreach (string name in new[] { "a.txt", "b.txt", "c.txt" })
    {
        await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + name });
    }
}
else if (which == "two-calls")
{
    await Curl(url + "a.txt");
    await Curl(url + "b.txt");
}
else
{
    string[] extra = which switch
    {
        "close" => ["-H", "Connection: close"],
        "http1.0" => ["--http1.0"],
        "rate" or "idle" => ["--rate", "1/2s"],
        _ => [],
    };
    string[] urls = which switch
    {
        "urls" => [url + "a.txt", url + "b.txt", url + "c.txt"],
        "http1.0" or "rate" or "idle" => [url + "{a,b}.txt"],
        _ => [url + "{a,b,c}.txt"],
    };
    await Curl([.. extra, .. urls]);
}

// get_exchanges: one exchange per connection, with the request heads it received.
JsonElement exchanges = (await RecordedAsync(EndsWithReply)).GetProperty("exchanges");
Console.WriteLine($"exchanges: {exchanges.GetArrayLength()}");
foreach (JsonElement exchange in exchanges.EnumerateArray())
{
    IEnumerable<string> requests = exchange.GetProperty("chunks").EnumerateArray()
        .Where(chunk => chunk.GetProperty("direction").GetString() == "received")
        .Select(chunk => chunk.GetProperty("data").GetString()!.Split("\r\n")[0]);
    Console.WriteLine($"exchange {exchange.GetProperty("exchange_id")}: {string.Join(", ", requests)}");
}

return 0;

// curl: one command line. -w prints, per transfer, its path and whether it opened a connection.
async Task Curl(params string[] tail)
{
    string[] line = ["-s", "--create-dirs", "-o", "got/#1.txt", "-w", "%{url.path} %{num_connects}\n", .. tail];
    if (!tail.Any(t => t.Contains('{')))
    {
        // No glob: one -o per URL.
        line = ["-s", "-w", "%{url.path} %{num_connects}\n", .. tail.SelectMany(t => t.StartsWith("http") ? new[] { "-o", "got/" + t[(t.LastIndexOf('/') + 1)..], t } : new[] { t })];
        line = [.. line.Prepend("--create-dirs")];
    }

    CallToolResult fetched = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = line });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
    foreach (string written in answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Split('\n'))
    {
        if (written.Trim().Length > 0)
        {
            Console.WriteLine(written.Trim());
        }
    }
}

// get_exchanges, asked again until the record holds what ready looks for and two answers 100 ms apart
// agree: the server's record can trail curl by a moment. At most 10 s, then the last answer.
async Task<JsonElement> RecordedAsync(Func<JsonElement, bool> ready, int after = 0)
{
    JsonElement last = default;
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["after_exchange_id"] = after });
        JsonElement answer = result.StructuredContent!.Value.Clone();
        if (ready(answer) && last.ValueKind != JsonValueKind.Undefined && answer.GetRawText() == last.GetRawText())
        {
            return answer;
        }

        last = answer;
    }

    return last;
}

// True once the newest exchange ends with something the server sent, as every finished exchange here does.
static bool EndsWithReply(JsonElement answer) =>
    answer.GetProperty("exchanges").EnumerateArray().LastOrDefault() is { ValueKind: JsonValueKind.Object } newest
    && newest.GetProperty("chunks").EnumerateArray().LastOrDefault() is { ValueKind: JsonValueKind.Object } final
    && final.GetProperty("direction").GetString() == "sent";
