// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a server with surl-mcp, lets curl-mcp make a request, and reads back the bytes
// that crossed the wire with get_exchanges.
// Run: dotnet run read-exchanges.cs -- <case>
//   all      one request from transfer, then every exchange (the default)
//   after    a second request from the curl tool, then the exchanges after the first
//   page     two requests, then one exchange at a time
//   filter   two requests, then only those through the http listener the server bound
//   unknown  get_exchanges for a server ID this host is not running
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "all";

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

// A server on a free port with one file in memory; its ID names it in every later call.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The client under test: one GET from transfer, and for some cases a second from the curl tool.
await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt" });
if (which is "after" or "page" or "filter")
{
    await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", "-A", "surly-reader", url + "hello.txt" } });
}

await RecordedAsync(answer => answer.GetProperty("exchanges").GetArrayLength() >= (which is "after" or "page" or "filter" ? 2 : 1) && EndsWithReply(answer));
var query = new Dictionary<string, object?> { ["server_id"] = which == "unknown" ? "s9" : id };
if (which == "after") query["after_exchange_id"] = 1;
if (which == "page") query["max_exchanges"] = 1;
if (which == "filter") { query["scheme"] = "HTTP"; query["listen_url"] = url; }
CallToolResult result = await surl.CallToolAsync("get_exchanges", query);
if (result.IsError == true)
{
    Console.WriteLine(result.Content.OfType<TextContentBlock>().Single().Text);
    return 1;
}

// Each exchange's first chunk is the request as received; its last is the end of the response.
// Data is printed as JSON spells it, so each carriage return and line feed shows as \r\n.
JsonElement answer = result.StructuredContent!.Value;
foreach (JsonElement exchange in answer.GetProperty("exchanges").EnumerateArray())
{
    Console.WriteLine($"exchange {exchange.GetProperty("exchange_id")}: {exchange.GetProperty("scheme")} via {exchange.GetProperty("listen_url")}");
    JsonElement[] chunks = exchange.GetProperty("chunks").EnumerateArray().ToArray();
    foreach (JsonElement chunk in new[] { chunks[0], chunks[^1] })
    {
        Console.WriteLine($"  {chunk.GetProperty("direction")} {chunk.GetProperty("layer")} {chunk.GetProperty("encoding")}: {chunk.GetProperty("data").GetRawText()}");
    }
}

Console.WriteLine($"has_more: {answer.GetProperty("has_more").GetRawText()}");
return 0;

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
