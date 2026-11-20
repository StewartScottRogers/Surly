// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent replays a topic on a surl Kafka broker from an offset: surl-mcp starts the broker and reads
// its log, curl-mcp consumes. Run: dotnet run replay.cs -- <case>
//   solution      a consume from the saved offset 3
//   whole-log     no offset, and offset 0
//   caught-up     an offset equal to the record count
//   past-the-end  an offset past the end
//   not-a-number  an offset that is not a number
//   read-log      the broker's own log, with offsets
//   fresh-broker  the saved offset against a new, empty broker
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

(string id, string url) = await Start();

// Five records: a, b and c through surl-mcp, d and e through curl, as two kinds of producer.
foreach (string value in new[] { "a", "b", "c" }) await Publish(value);
await Run("-d", "d", url + "orders");
await Run("-d", "e", url + "orders");

switch (which)
{
    case "solution":
        await Curl(url + "orders?offset=3");
        break;
    case "whole-log":
        await Curl(url + "orders");
        await Curl(url + "orders?offset=0");
        break;
    case "caught-up":
        await Curl(url + "orders?offset=5");
        await Publish("f");
        await Curl(url + "orders?offset=5");
        break;
    case "past-the-end":
        await Curl(url + "orders?offset=9");
        break;
    case "not-a-number":
        await Curl(url + "orders?offset=abc");
        break;
    case "read-log":
        await ReadLog(3);
        await ReadLog(0, limit: 2);
        await ReadLog(9);
        break;
    case "fresh-broker":
        (_, url) = await Start();
        await Curl(url + "orders?offset=5");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// start_server answers server_id and bound_urls; the broker takes anonymous clients, for a test.
async Task<(string, string)> Start()
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "kafka://127.0.0.1:0" },
        ["arguments"] = new[] { "--allow-anonymous" },
    });
    JsonElement content = started.StructuredContent!.Value;
    return (content.GetProperty("server_id").GetString()!, content.GetProperty("bound_urls")[0].GetString()!);
}

// publish_message appends a record to the topic, as a producer would.
async Task Publish(string payload) => await surl.CallToolAsync("publish_message", new Dictionary<string, object?>
{
    ["server_id"] = id, ["topic"] = "orders", ["payload"] = payload,
});

// read_log reads the topic from an offset, with the offsets that curl never prints.
async Task ReadLog(int from, int? limit = null)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = id, ["topic"] = "orders", ["from_offset"] = from };
    if (limit is { } n) arguments["limit"] = n;
    CallToolResult result = await surl.CallToolAsync("read_log", arguments);
    if (result.IsError == true)
    {
        Console.WriteLine($"read_log {from}: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
        return;
    }
    JsonElement page = result.StructuredContent!.Value;
    string records = string.Join(" ", page.GetProperty("records").EnumerateArray()
        .Select(r => $"{r.GetProperty("offset").GetInt32()}={r.GetProperty("text").GetString()}"));
    Console.WriteLine($"read_log {from}: {records}, next {page.GetProperty("next_offset").GetInt32()}, more {page.GetProperty("has_more").GetBoolean()}");
}

// The curl tool runs curl's own arguments, as the console would. Without -d, a kafka: URL consumes.
// -sS keeps the progress meter out and the error in. Its answer is text, as JSON.
async Task<(bool, JsonElement)> Run(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = arguments.Prepend("-sS").ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return (result.IsError == true, answer.RootElement.Clone());
}

async Task Curl(params string[] arguments)
{
    (bool toolError, JsonElement root) = await Run(arguments);
    string output = root.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\n", "|");
    string error = root.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    Console.WriteLine($"curl exit {root.GetProperty("exitCode").GetInt32()}, tool error {(toolError ? "yes" : "no")}, output [{output}]{(error == "" ? "" : $", error {error}")}");
}
