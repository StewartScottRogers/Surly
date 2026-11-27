// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent stands in for a NATS service: surl-mcp is the NATS server, a curl-mcp subscriber listens on
// prices.get, the app's request is a second curl-mcp call, and the agent reads the request's reply subject
// from the server's record and answers it with publish_message.
// Run: dotnet run program.cs -- <scenario>
//   solution   the agent answers price=9.99
//   none       nothing is subscribed to prices.get
//   silent     the subscriber receives the request and the agent never answers
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "nats://127.0.0.1:0" }, ["arguments"] = new[] { "--allow-anonymous" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string nats = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

if (scenario == "none")
{
    (int code, string _, string error) = await Curl("-sS", "-m", "10", "-d", "sku=A1", $"{nats}prices.get?request");
    Console.WriteLine($"app: exit {code}, {error}");
    return 0;
}

// The listening side: a subscriber to prices.get that ends after one message. The call runs while the agent
// goes on; the server's record shows its SUB once the server has it.
Task<(int, string, string)> listener = Curl("-sS", "-m", "20", $"{nats}prices.get?count=1");
await Find("received", @"SUB prices\.get 1\r\n");

// The app: a request with a reply subject of curl's own, which waits for one reply.
Task<(int, string, string)> app = Curl("-sS", "-m", scenario == "silent" ? "3" : "10", "-d", "sku=A1", $"{nats}prices.get?request");
Match request = await Find("received", @"PUB prices\.get (_INBOX\.[0-9a-f]+) 6\r\n");
string inbox = request.Groups[1].Value;
(_, string asked, _) = await listener;

if (scenario == "silent")
{
    Console.WriteLine($"the subscriber received: {asked.TrimEnd()}");
    (int code, string _, string error) = await app;
    Console.WriteLine($"app: exit {code}, {error}");
    return 0;
}

// The answer: publish_message puts price=9.99 on the bus under the reply subject, written as a topic.
await surl.CallToolAsync("publish_message", new Dictionary<string, object?>
{
    ["server_id"] = id, ["topic"] = inbox.Replace('.', '/'), ["payload"] = "price=9.99",
});
(int exit, string got, _) = await app;
Console.WriteLine($"app got: {got} (exit {exit})");
Console.WriteLine($"the app asked: {asked.TrimEnd()}");
Console.WriteLine($"reply subject: {inbox}");

// The server's record of both sides: the app's PUB it received, and the reply it sent the app as a MSG on the inbox.
Console.WriteLine($"server record, the app's request: {request.Value.TrimEnd()}");
Match reply = await Find("sent", Regex.Escape($"MSG {inbox} ") + @"\d+ 10\r\nprice=9\.99\r\n");
Console.WriteLine($"server record, the reply sent to the app: {reply.Value.Split('\r')[0]}");
return 0;

// curl-mcp's curl tool runs curl with the arguments given; its answer is text, as JSON.
async Task<(int, string, string)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(), answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim());
}

// The server's record: get_exchanges gives what each client sent (received) and what the server sent it (sent),
// chunk by chunk. It is written as the server works, so look for the pattern every 100 ms, for at most 10 s.
async Task<Match> Find(string direction, string pattern)
{
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        CallToolResult record = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["max_exchanges"] = 100 });
        foreach (JsonElement exchange in record.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
        {
            var received = new StringBuilder();
            foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray().Where(c => c.GetProperty("direction").GetString() == direction))
            {
                string data = chunk.GetProperty("data").GetString()!;
                received.Append(chunk.TryGetProperty("encoding", out JsonElement e) && e.GetString() == "base64"
                    ? Encoding.UTF8.GetString(Convert.FromBase64String(data)) : data);
            }

            Match found = Regex.Match(received.ToString(), pattern);
            if (found.Success)
            {
                return found;
            }
        }
    }

    throw new TimeoutException($"the server's record held no {direction} {pattern} after 10 s");
}
