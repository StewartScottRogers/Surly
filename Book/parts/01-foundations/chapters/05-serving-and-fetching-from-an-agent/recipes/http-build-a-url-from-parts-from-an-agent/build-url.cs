// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent assembles a URL from its parts with curl-mcp's build_url, then fetches it with transfer
// from a server it started with surl-mcp, and reads back the request line the server received.
// Run: dotnet run build-url.cs -- <case>
//   encode     path and query encoded: UrlEncode and AppendQuery (the default)
//   raw        the same parts with no flags, then fetched as built
//   equals     UrlEncode without AppendQuery: the query's '=' is encoded too
//   bad-port   a port that is not a number
//   no-scheme  the parts without a scheme
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "encode";

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

// surl-mcp: a server on a free port with a file whose name has a space in it.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["reports/q3 sales.csv"] = "region,total\nnorth east,42\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
var bound = new Uri(server.GetProperty("bound_urls")[0].GetString()!);

// The parts, as an agent has them: no encoding done by hand.
var parts = new Dictionary<string, object?>
{
    ["scheme"] = which == "no-scheme" ? null : "http",
    ["host"] = bound.Host,
    ["port"] = which == "bad-port" ? "eighty" : bound.Port.ToString(),
    ["path"] = "/reports/q3 sales.csv",
    ["query"] = "region=north east",
};
if (which is "encode" or "bad-port" or "no-scheme") parts["flags"] = new[] { "UrlEncode", "AppendQuery" };
if (which == "equals") parts["flags"] = new[] { "UrlEncode" };

// curl-mcp answers in text, as JSON: the URL, or a failure naming the part the parser refused.
CallToolResult built = await curl.CallToolAsync("build_url", parts);
JsonElement answer = JsonDocument.Parse(built.Content.OfType<TextContentBlock>().Single().Text).RootElement;
if (built.IsError == true)
{
    JsonElement failure = answer.GetProperty("failure");
    Console.WriteLine($"build_url failed: {failure.GetProperty("kind")}: {failure.GetProperty("text")}");
    Console.WriteLine("tool error: yes");
    return 1;
}

string url = answer.GetProperty("url").GetString()!;
Console.WriteLine($"url: {url.Replace(bound.Port.ToString(), "PORT")}");
if (which == "equals")
{
    return 0;
}

// Fetch what was built, then ask the server what request line it received.
CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
JsonElement got = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
if (fetched.IsError == true)
{
    JsonElement failure = got.GetProperty("failure");
    Console.WriteLine($"transfer failed: {failure.GetProperty("kind")}, exit code {failure.GetProperty("exitCode")}");
}
else
{
    Console.WriteLine($"statusCode: {got.GetProperty("statusCode")}");
}

JsonElement exchanges = (await RecordedAsync(answer => fetched.IsError == true || EndsWithReply(answer))).GetProperty("exchanges");
Console.WriteLine($"requests the server received: {exchanges.GetArrayLength()}");
foreach (JsonElement exchange in exchanges.EnumerateArray())
{
    string head = exchange.GetProperty("chunks").EnumerateArray().First(c => c.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!;
    Console.WriteLine($"  {head[..head.IndexOf('\r')]}");
}

Console.WriteLine($"tool error: {(fetched.IsError == true ? "yes" : "no")}");
return fetched.IsError == true ? 1 : 0;

// get_exchanges, asked again until the record holds what ready looks for and two answers 100 ms apart
// agree: the server's record can trail curl by a moment. At most 10 s, then the last answer.
async Task<JsonElement> RecordedAsync(Func<JsonElement, bool> ready, int after = 0)
{
    JsonElement last = default;
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId, ["after_exchange_id"] = after });
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
