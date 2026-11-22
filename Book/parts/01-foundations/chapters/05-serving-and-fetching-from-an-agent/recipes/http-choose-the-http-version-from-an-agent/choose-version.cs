// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches one file from a surl-mcp server in HTTP/1.0, 1.1 and 2 with prior knowledge,
// through curl-mcp's curl tool, and reports the version that answered and the request line the
// server received.
// Run: dotnet run choose-version.cs [-- transfer]
//   (no argument) the curl tool with --http1.0, --http1.1 and --http2-prior-knowledge
//   transfer      the transfer tool, which has no version field
#:package ModelContextProtocol.Core@*

using System.Text;
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

// surl-mcp: one listener, which speaks HTTP/1.x and HTTP/2 alike.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

if (args is ["transfer"])
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"transfer: answered in {answer.GetProperty("info").GetProperty("httpVersion")}");
    Console.WriteLine($"  server received: {await LastRequestLine()}");
    return 0;
}

foreach (string option in new[] { "--http1.0", "--http1.1", "--http2-prior-knowledge" })
{
    // curl-mcp's curl tool: the same arguments as the command line; -w prints the version that answered.
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-o", "body.txt", "-w", "%{http_version}", option, url },
    });
    JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{option}: exit {answer.GetProperty("exitCode")}, answered in {answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}");
    Console.WriteLine($"  server received: {await LastRequestLine()}");
}

return 0;

// surl-mcp's get_exchanges: the first chunk the server received in the newest exchange holds the
// request; text for HTTP/1.x, base64 for HTTP/2's binary frames.
async Task<string> LastRequestLine()
{
    JsonElement last = (await RecordedAsync(answer => answer.GetProperty("exchanges").GetArrayLength() > 0)).GetProperty("exchanges").EnumerateArray().Last();
    JsonElement chunk = last.GetProperty("chunks").EnumerateArray().First(c => c.GetProperty("direction").GetString() == "received");
    string data = chunk.GetProperty("data").GetString()!;
    string text = chunk.GetProperty("encoding").GetString() == "base64" ? Encoding.ASCII.GetString(Convert.FromBase64String(data)) : data;
    return text[..text.IndexOf('\r')];
}

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
