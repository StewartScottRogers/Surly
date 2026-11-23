// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts one surl that speaks STOMP and MQTT 5 with surl-mcp, runs the new service (an MQTT 5 subscriber) and
// the old one (STOMP sends) with curl-mcp's curl tool, shows a message crossing, and reads what the broker kept,
// over the official MCP SDK.
// Run: dotnet run join.cs -- <case>
//   solution        two orders sent to /topic/orders/created; what the new service on orders/created received
//   whole           the same, every message the new service received
//   kept            the same, then the topic's retained message, read with read_retained_message
//   stomp-retained  the same, then a STOMP subscriber to the topic
//   dotted          the old service sends to /topic/orders.created
//   queue           the old service sends to /queue/orders/created
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args[0];

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

// surl-mcp: one broker for both services, STOMP and MQTT 5 on one message bus, keeping the latest message under
// orders/ and ending an idle STOMP subscription after a second.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "stomp://127.0.0.1:0", "mqtt5://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous", "--topic", "orders/#;retain", "--idle-timeout", "1" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string stomp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string mqtt5 = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

string destination = which switch
{
    "dotted" => "topic/orders.created",
    "queue" => "queue/orders/created",
    _ => "topic/orders/created",
};
string[]? received = await Join(destination, "orders/created");
if (received is null)
{
    Console.WriteLine($"nothing crossed from /{destination} to orders/created");
    return 1;
}

switch (which)
{
    case "whole":
        Console.Write(string.Concat(received.Select(line => line + "\n")));
        break;
    case "kept":
        // The broker's own record: the topic's retained message, read without a client. The store may trail the
        // subscriber a moment, so look for at most ten seconds.
        JsonElement kept = default;
        for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(10); await Task.Delay(200))
        {
            CallToolResult read = await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
            {
                ["server_id"] = serverId, ["topic"] = "orders/created",
            });
            kept = read.StructuredContent!.Value.Clone();
            if (kept.TryGetProperty("text", out JsonElement text) && text.GetString() == "order 1002\n")
            {
                break;
            }
        }

        Console.Write($"kept on orders/created: {(kept.TryGetProperty("text", out JsonElement found) ? found.GetString() : "nothing\n")}");
        break;
    case "stomp-retained":
        // A STOMP subscriber to the same topic; surl ends the idle subscription, and -m is only a ceiling.
        JsonElement subscribed = await Curl("-s", "-S", "-m", "10", stomp + "topic/orders/created");
        Console.WriteLine($"STOMP subscriber: exit code {subscribed.GetProperty("exitCode")}, " +
                          $"{subscribed.GetProperty("standardOutput").GetProperty("content").GetString()!.Length} characters");
        break;
    default:
        Console.Write(string.Concat(received.Where(line => line.StartsWith("order ")).Select(line => line + "\n")));
        break;
}

return 0;

// The test: starts the new service, has the old one send ping until the new one has it (ten tries a second apart at
// most), then two orders, and waits, ten seconds at most, for the second. Answers every line the new service
// received, or null if nothing crossed.
async Task<string[]?> Join(string from, string topic)
{
    File.Delete(Path.Combine(here, "got.txt"));

    // The new service: an MQTT 5 subscriber stays subscribed and writes each message to got.txt as it arrives.
    // The call is left running while the agent goes on; -m 30 ends it if nothing else does.
    _ = curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-m", "30", "-o", "got.txt", mqtt5 + topic },
    }).AsTask();

    // got.txt as far as it is written; curl-mcp still has it open, so share it, and an absent file is nothing yet.
    string[] Received()
    {
        try
        {
            using var file = new FileStream(Path.Combine(here, "got.txt"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return new StreamReader(file).ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (IOException)
        {
            return [];
        }
    }

    bool joined = false;
    for (int attempt = 0; attempt < 10 && !joined; attempt++)
    {
        await Send(from, "ping\n");
        for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(1) && !(joined = Received().Contains("ping"));)
        {
            await Task.Delay(50);
        }
    }

    if (!joined)
    {
        return null;
    }

    await Send(from, "order 1001\n");
    await Send(from, "order 1002\n");
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(10) && !Received().Contains("order 1002");)
    {
        await Task.Delay(50);
    }

    return Received();
}

// The old service: the curl tool sends the message over STOMP, read from its standard input to keep the line feed.
async Task Send(string to, string message)
{
    await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "--data-binary", "@-", stomp + to },
        ["stdin"] = message,
    });
}

async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
