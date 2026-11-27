// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent sends custom headers with curl-mcp's transfer and reads back, with surl-mcp's
// get_exchanges, the request head the server received.
// Run: dotnet run check-headers.cs -- <case>
//   sent        the API key and a trace ID (the default)
//   agent       the same, with transfer's userAgent field set
//   twice       the trace ID given twice
//   empty       the API key with an empty value
//   empty-sent  the API key with an empty value and sendEmpty
//   no-accept   an Accept header with an empty value
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "sent";

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

// A server on a free port with one file in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The request: headers is a list of name and value pairs, sent in that order.
// An empty value removes the header; sendEmpty true sends it with nothing after the colon.
JsonObject Header(string name, string value, bool sendEmpty = false) =>
    sendEmpty ? new() { ["name"] = name, ["value"] = value, ["sendEmpty"] = true } : new() { ["name"] = name, ["value"] = value };
var headers = new JsonArray(
    which switch { "empty" => Header("X-Api-Key", ""), "empty-sent" => Header("X-Api-Key", "", sendEmpty: true), _ => Header("X-Api-Key", "k123") },
    Header("X-Trace-Id", "7f3a9c"));
if (which == "twice") headers.Add((JsonNode)Header("X-Trace-Id", "again"));
if (which == "no-accept") headers.Add((JsonNode)Header("Accept", ""));
var request = new Dictionary<string, object?> { ["url"] = url + "hello.txt", ["headers"] = headers };
if (which == "agent") request["userAgent"] = "billing/2.1";
CallToolResult fetched = await curl.CallToolAsync("transfer", request);
using JsonDocument answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text);
Console.WriteLine($"statusCode: {answer.RootElement.GetProperty("statusCode").GetInt32()}");

// What arrived: the first received chunk of the exchange holds the request head.
JsonElement exchange = (await RecordedAsync(EndsWithReply)).GetProperty("exchanges")[0];
JsonElement received = exchange.GetProperty("chunks").EnumerateArray().First(c => c.GetProperty("direction").GetString() == "received");
string head = received.GetProperty("data").GetString()!.Split("\r\n\r\n")[0];
foreach (string line in head.Split("\r\n"))
{
    Console.WriteLine($"> {line}");
}

return fetched.IsError == true ? 1 : 0;

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
