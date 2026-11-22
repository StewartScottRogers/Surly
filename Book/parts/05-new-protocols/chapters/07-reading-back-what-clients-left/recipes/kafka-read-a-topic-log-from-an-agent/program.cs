// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reads the log a producer under test wrote to a Kafka topic, by offset and without consuming it, with
// surl-mcp's read_log, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   log       the producer writes four records; the agent reads the whole log (the default)
//   from      the agent reads from offset 2
//   paging    the agent reads two records at a time
//   replay    a consumer reads the topic after the agent has
//   past-end  the agent reads from an offset beyond the log
//   no-topic  the agent reads a topic nobody wrote to
//   closed    a broker with no --allow-anonymous
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "log";

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

// A Kafka broker. Every topic has one partition. --allow-anonymous lets the producer in with no login.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "kafka://127.0.0.1:0" },
    ["arguments"] = which == "closed" ? Array.Empty<string>() : new[] { "--allow-anonymous" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string orders = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "orders";

// The producer under test: one record for each step of an order, written with curl.
if (which != "no-topic")
{
    foreach (string step in new[] { "order-1001:placed", "order-1001:paid", "order-1001:packed", "order-1001:shipped" })
    {
        (int exit, _, string error) = await Curl("-d", step, orders);
        if (exit != 0)
        {
            Console.WriteLine($"producer could not write: exit {exit} ({error})");
            break;
        }
    }
}

switch (which)
{
    case "from":
        Print(await Read("orders", 2, 100));
        break;
    case "paging":
        long next = 0;
        while (true)
        {
            JsonElement page = await Read("orders", next, 2);
            Print(page, $"page from {next}");
            if (!page.GetProperty("has_more").GetBoolean())
            {
                break;
            }

            next = page.GetProperty("next_offset").GetInt64();
        }

        break;
    case "replay":
        Print(await Read("orders", 0, 100), "read_log");
        // A consumer starts where it likes: the records are still there.
        (_, string consumed, _) = await Curl(orders + "?offset=2");
        Console.WriteLine($"consumer from offset 2: {consumed.Replace("\r", "").Replace("\n", ", ").TrimEnd(',', ' ')}");
        Print(await Read("orders", 0, 100), "read_log again");
        break;
    case "past-end":
        await Refused("orders", 9);
        break;
    case "no-topic":
        await Refused("refunds", 0);
        break;
    case "closed":
        await Refused("orders", 0);
        break;
    default:
        Print(await Read("orders", 0, 100));
        break;
}

return 0;

// read_log reads the log of a topic from an offset, as many records as the limit, and takes nothing.
async Task<JsonElement> Read(string topic, long fromOffset, int limit)
{
    CallToolResult result = await surl.CallToolAsync("read_log", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic, ["from_offset"] = fromOffset, ["limit"] = limit,
    });
    return result.StructuredContent!.Value;
}

async Task Refused(string topic, long fromOffset)
{
    CallToolResult result = await surl.CallToolAsync("read_log", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic, ["from_offset"] = fromOffset,
    });
    Console.WriteLine($"read_log of {topic} from {fromOffset}: tool error {result.IsError}, {result.Content.OfType<TextContentBlock>().Single().Text}");
}

void Print(JsonElement page, string label = "read_log")
{
    JsonElement[] records = page.GetProperty("records").EnumerateArray().ToArray();
    Console.WriteLine($"{label}: {records.Length} records, next_offset {page.GetProperty("next_offset")}, has_more {page.GetProperty("has_more")}");
    foreach (JsonElement record in records)
    {
        Console.WriteLine($"  {record.GetProperty("offset")} {record.GetProperty("text").GetString()}");
    }
}

// The curl tool runs curl's own arguments; -s keeps the progress meter out and -S puts an error message back.
async Task<(int Exit, string Output, string Error)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "--max-time", "20" }.Concat(curlArguments).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(),
        answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd());
}
