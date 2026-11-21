// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks that a consumer saw every record of a Kafka topic: curl-mcp is the consumer and what it
// reports is its output, surl-mcp reads the broker log, and the agent compares the two and names what the
// consumer missed. Run: dotnet run check-consumer.cs -- <scenario>
//   solution      the consumer reads from offset 0 and sees all six records
//   skipped       the consumer starts from offset 2, as if it had saved a wrong offset
//   late          the consumer ends after four records; two more are produced after it
//   pages         the log read three records at a time
//   kept          the log read before and after the consumer
//   past-the-end  a read of the log from an offset past the end
//   no-topic      a read of a topic nobody wrote to
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "solution";

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

// surl-mcp: a Kafka broker that takes anonymous clients, for a test.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "kafka://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The producers: the first three records through surl-mcp, the next through curl, as two kinds of producer.
// A Kafka produce is acknowledged by the broker before it returns, so the log holds each record at once.
foreach (string value in new[] { "evt-1", "evt-2", "evt-3" })
{
    await surl.CallToolAsync("publish_message", new Dictionary<string, object?> { ["server_id"] = serverId, ["topic"] = "orders", ["payload"] = value });
}

int produced = scenario == "late" ? 4 : 6;
for (int number = 4; number <= produced; number++)
{
    await Curl("-sS", "-d", $"evt-{number}", url + "orders");
}

switch (scenario)
{
    case "solution":
    case "skipped":
    case "late":
        // What the consumer reports is what the curl tool consumes: each value, a newline after each.
        string[] reported = await Consume(scenario == "skipped" ? 2 : 0);
        Console.WriteLine($"consumer reported: {string.Join(", ", reported)}");
        if (scenario == "late")
        {
            await Curl("-sS", "-d", "evt-5", url + "orders");
            await Curl("-sS", "-d", "evt-6", url + "orders");
        }

        var log = await ReadAll();
        string[] missed = [.. log.Where(record => !reported.Contains(record.Text)).Select(record => $"offset {record.Offset} {record.Text}")];
        Console.WriteLine($"the log holds {log.Count} records");
        Console.WriteLine(missed.Length == 0 ? "the consumer saw every record" : $"the consumer missed {string.Join(", ", missed)}");
        return missed.Length == 0 ? 0 : 1;
    case "pages":
        long next = 0;
        bool more = true;
        while (more)
        {
            JsonElement page = await ReadLog(next, limit: 3);
            Console.WriteLine($"page from {next}: {string.Join(", ", page.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("text").GetString()))}, next {page.GetProperty("next_offset").GetInt64()}, more {page.GetProperty("has_more").GetBoolean()}");
            next = page.GetProperty("next_offset").GetInt64();
            more = page.GetProperty("has_more").GetBoolean();
        }

        break;
    case "kept":
        Console.WriteLine($"before the consumer: {(await ReadAll()).Count} records");
        Console.WriteLine($"the consumer reported: {(await Consume(0)).Length} records");
        Console.WriteLine($"after the consumer: {(await ReadAll()).Count} records");
        break;
    case "past-the-end":
        await Show("orders", 9);
        break;
    case "no-topic":
        await Show("refunds", 0);
        break;
}

return 0;

// The curl tool runs curl with the arguments given, as the console would; its answer is text, as JSON.
async Task<string> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
}

// A consume from an offset writes each value and a newline, to the end of the log.
async Task<string[]> Consume(int offset) =>
    (await Curl("-sS", $"{url}orders?offset={offset}")).Split('\n', StringSplitOptions.RemoveEmptyEntries);

// read_log is the broker's own view of the topic: offsets and text, a page at a time.
async Task<JsonElement> ReadLog(long from, int limit)
{
    CallToolResult result = await surl.CallToolAsync("read_log", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["topic"] = "orders", ["from_offset"] = from, ["limit"] = limit,
    });
    return result.StructuredContent!.Value.Clone();
}

// The whole log: pages are followed with next_offset until has_more is false.
async Task<List<(long Offset, string Text)>> ReadAll()
{
    var all = new List<(long, string)>();
    long next = 0;
    bool more = true;
    while (more)
    {
        JsonElement page = await ReadLog(next, limit: 100);
        all.AddRange(page.GetProperty("records").EnumerateArray().Select(r => (r.GetProperty("offset").GetInt64(), r.GetProperty("text").GetString()!)));
        next = page.GetProperty("next_offset").GetInt64();
        more = page.GetProperty("has_more").GetBoolean();
    }

    return all;
}

// A read the broker refuses is a tool error whose text names the log.
async Task Show(string topic, long from)
{
    CallToolResult result = await surl.CallToolAsync("read_log", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["topic"] = topic, ["from_offset"] = from,
    });
    Console.WriteLine($"read_log {topic} from {from}: tool error {(result.IsError == true ? "yes" : "no")}, {result.Content.OfType<TextContentBlock>().Single().Text}");
}
