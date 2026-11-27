// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent puts messages on a queue declared at start and lists them without taking any: surl-mcp runs a broker
// with --queue jobs, publish_message queues three jobs, and browse_queue lists them.
// Run: dotnet run browse.cs -- <case>
//   browse      queue three jobs, browse twice (the default)
//   page        the same, browsed two at a time
//   binary      queue a payload that is not text
//   topic       publish to the topic jobs instead of the queue
//   undeclared  queue to a broker started without --queue jobs
//   reset       queue three jobs, then reset the server
#:package ModelContextProtocol.Core@2.4.0

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "browse";

// surl-mcp, confined to this folder by SURL_MCP_ROOT. This agent needs no client, so no curl-mcp.
string here = Directory.GetCurrentDirectory();
await using McpClient surl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl-mcp", Command = "surl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["SURL_MCP_ROOT"] = here },
}));

// An MQTT broker whose message bus holds the queue jobs from the start.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
    ["arguments"] = mode == "undeclared" ? Array.Empty<string>() : new[] { "--queue", "jobs" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;

switch (mode)
{
    case "binary":
        Print("publish_message", await Call("publish_message", new() { ["queue"] = "jobs", ["payload_base64"] = "AP8QgA==" }));
        Print("browse_queue", await Call("browse_queue", new() { ["queue"] = "jobs" }));
        return 0;
    case "topic":
        Print("publish_message", await Call("publish_message", new() { ["topic"] = "jobs", ["payload"] = "resize cat.png", ["retain"] = true }));
        Print("browse_queue", await Call("browse_queue", new() { ["queue"] = "jobs" }));
        Print("read_retained_message", await Call("read_retained_message", new() { ["topic"] = "jobs" }));
        return 0;
    case "undeclared":
        Print("publish_message", await Call("publish_message", new() { ["queue"] = "jobs", ["payload"] = "resize cat.png" }));
        Print("browse_queue", await Call("browse_queue", new() { ["queue"] = "jobs" }));
        return 0;
}

// Three jobs, in order.
foreach (string job in new[] { "resize cat.png", "resize dog.png", "mail ann" })
{
    Print("publish_message", await Call("publish_message", new() { ["queue"] = "jobs", ["payload"] = job }));
}

switch (mode)
{
    case "page":
        Print("browse_queue limit 2", await Call("browse_queue", new() { ["queue"] = "jobs", ["limit"] = 2 }));
        Print("browse_queue after 1", await Call("browse_queue", new() { ["queue"] = "jobs", ["after_position"] = 1, ["limit"] = 2 }));
        break;
    case "reset":
        Print("reset_server", await Call("reset_server", new()));
        Print("browse_queue", await Call("browse_queue", new() { ["queue"] = "jobs" }));
        break;
    default:
        Print("browse_queue", await Call("browse_queue", new() { ["queue"] = "jobs" }));
        Print("browse_queue again", await Call("browse_queue", new() { ["queue"] = "jobs" }));
        break;
}

return 0;

// A surl-mcp call on this server: its structured answer, or the text of a tool error.
async Task<string> Call(string tool, Dictionary<string, object?> arguments)
{
    arguments["server_id"] = id;
    CallToolResult result = await surl.CallToolAsync(tool, arguments);
    return result.IsError == true
        ? "tool error: " + result.Content.OfType<TextContentBlock>().First().Text
        : result.StructuredContent!.Value.GetRawText();
}

static void Print(string what, string answer) => Console.WriteLine($"{what}: {answer}");
