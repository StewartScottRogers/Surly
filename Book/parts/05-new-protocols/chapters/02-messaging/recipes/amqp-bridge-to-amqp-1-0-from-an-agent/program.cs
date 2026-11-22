// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent bridges AMQP 0-9-1 and AMQP 1.0 on one surl-mcp server: curl-mcp sends and receives over each
// version, and surl-mcp says when a message has reached the queue.
// Run: dotnet run program.cs -- <scenario>
//   solution  send over 0-9-1, receive over 1.0, then look again over 0-9-1
//   reverse   send over 1.0, receive over 0-9-1
//   order     one message from each version, received in the order sent
//   address   a different address holds nothing; the message is still on orders
//   wrong     a wrong password on each listener
//   locked    a server started without --allow-plaintext-auth
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

// surl-mcp: one server, an AMQP 0-9-1 listener and an AMQP 1.0 listener, one account, one message bus.
var arguments = new List<string> { "--user", "alice:s3cret" };
if (scenario != "locked")
{
    arguments.Add("--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "amqp://127.0.0.1:0", "amqp1://127.0.0.1:0" },
    ["arguments"] = arguments,
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string amqp091 = server.GetProperty("bound_urls")[0].GetString()! + "%2F/";   // %2F is the virtual host /
string amqp1 = server.GetProperty("bound_urls")[1].GetString()!;               // AMQP 1.0 has only an address
long lastSequence = 0;

switch (scenario)
{
    case "solution":
        await Send("0-9-1", "hello");
        await Receive("over 1.0", "1.0", "orders");
        await Receive("over 0-9-1 after", "0-9-1", "orders");
        await Browse();
        break;
    case "reverse":
        await Send("1.0", "back");
        await Receive("over 0-9-1", "0-9-1", "orders");
        break;
    case "order":
        await Send("0-9-1", "one");
        await Send("1.0", "two");
        await Receive("first over 0-9-1", "0-9-1", "orders");
        await Receive("second over 0-9-1", "0-9-1", "orders");
        break;
    case "address":
        await Send("0-9-1", "only-orders");
        await Receive("refunds over 1.0", "1.0", "refunds", wait: 2);
        await Receive("orders over 1.0", "1.0", "orders");
        break;
    case "wrong":
        await Receive("1.0", "1.0", "orders", password: "wrong");
        await Receive("0-9-1", "0-9-1", "orders", password: "wrong");
        break;
    case "locked":
        await Receive("1.0", "1.0", "orders");
        await Receive("0-9-1", "0-9-1", "orders");
        break;
}

return 0;

string Url(string version, string queue) => version == "0-9-1" ? amqp091 + queue : amqp1 + queue;

// curl-mcp's curl tool runs curl with the arguments given: -d is a send, no -d a receive. An AMQP 1.0
// receive from an empty address waits, so every receive carries -m, curl's time limit in seconds.
async Task Send(string version, string text)
{
    await Curl(["-sS", "-u", "alice:s3cret", "-d", text, Url(version, "orders")]);

    // The send is asynchronous: wait, with a bound, for the bus to say the message is on the queue.
    for (int attempt = 0; attempt < 10; attempt++)
    {
        CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = serverId, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus",
            ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
        });
        JsonElement found = waited.StructuredContent!.Value;
        if (!found.TryGetProperty("event", out JsonElement change))
        {
            throw new TimeoutException("the bus did not report the message in 10 s");
        }

        lastSequence = change.GetProperty("sequence").GetInt64();
        if (change.GetProperty("change").GetString() == "enqueued")
        {
            return;
        }
    }

    throw new TimeoutException("no enqueued event");
}

async Task Receive(string label, string version, string queue, int wait = 5, string password = "s3cret")
{
    (int exitCode, bool toolError, string output, string error) = await Curl(["-sS", "-m", wait.ToString(), "-u", $"alice:{password}", Url(version, queue)]);
    string line = $"{label}: exit {exitCode}, tool error {(toolError ? "yes" : "no")}, output [{output}]";
    Console.WriteLine(error.Length == 0 ? line : $"{line}, error {error}");
}

// browse_queue lists what is ready on the queue without taking it, and counts what is in flight.
// The receivers have gone, but the server settles what they took only once it has noticed: browse
// again until nothing is in flight, for at most 10 s.
async Task Browse()
{
    JsonElement page = await Page();
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); page.GetProperty("in_flight_count").GetInt32() > 0 && DateTime.UtcNow < deadline; page = await Page())
    {
        await Task.Delay(100);
    }

    Console.WriteLine($"queue orders: ready {page.GetProperty("ready_count").GetInt32()}, in flight {page.GetProperty("in_flight_count").GetInt32()}");
}

async Task<JsonElement> Page()
{
    CallToolResult browsed = await surl.CallToolAsync("browse_queue", new Dictionary<string, object?> { ["server_id"] = serverId, ["queue"] = "orders" });
    return browsed.StructuredContent!.Value.Clone();
}

async Task<(int, bool, string, string)> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    string error = root.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    return (root.GetProperty("exitCode").GetInt32(), result.IsError == true,
        root.GetProperty("standardOutput").GetProperty("content").GetString()!, error);
}
