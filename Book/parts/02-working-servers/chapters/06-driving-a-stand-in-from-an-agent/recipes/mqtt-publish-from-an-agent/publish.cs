// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent publishes on a surl MQTT broker through surl-mcp, reads back what the broker kept,
// and subscribes with curl through curl-mcp. Run: dotnet run publish.cs -- <case>
//   solution        a retained publish, read back with read_retained_message
//   not-retained    a publish without retain
//   empty-clears    a retained publish with an empty payload after a retained one
//   no-wildcards    a publish to sensors/+
//   client-refused  curl subscribing with no login
//   subscriber      curl subscribing on a broker started with --allow-anonymous
//   client-publish  curl publishing without retain, then read_retained_message
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

// An MQTT broker on a free port. With no accounts it lets no client in, unless --allow-anonymous.
bool open = which is "subscriber" or "client-publish";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "mqtt://127.0.0.1:0" },
    ["arguments"] = open ? new[] { "--allow-anonymous" } : Array.Empty<string>(),
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
if (open)
{
    Console.WriteLine(started.StructuredContent!.Value.GetProperty("log_lines")[0].GetString());
}

switch (which)
{
    case "solution":
        await Publish("sensors/1", "21.5", retain: true);
        await ReadRetained("sensors/1");
        break;
    case "not-retained":
        await Publish("sensors/2", "19.0", retain: false);
        await ReadRetained("sensors/2");
        break;
    case "empty-clears":
        await Publish("sensors/1", "21.5", retain: true);
        await Publish("sensors/1", "", retain: true);
        await ReadRetained("sensors/1");
        break;
    case "no-wildcards":
        await Publish("sensors/+", "0", retain: false);
        break;
    case "client-refused":
        await Publish("sensors/1", "21.5", retain: true);
        await Curl(url + "sensors/1");
        break;
    case "subscriber":
        await Publish("sensors/1", "21.5", retain: true);
        await Curl(url + "sensors/1");
        break;
    case "client-publish":
        await Curl("-d", "22.0", url + "sensors/3");
        await ReadRetained("sensors/3");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// publish_message answers {"published":true}, or is a tool error that says why not.
async Task Publish(string topic, string payload, bool retain) => Report("publish_message " + topic,
    await surl.CallToolAsync("publish_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic, ["payload"] = payload, ["retain"] = retain,
    }));

// read_retained_message answers the message the broker kept for a topic, or {"retained":false}.
async Task ReadRetained(string topic) => Report("read_retained_message " + topic,
    await surl.CallToolAsync("read_retained_message", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = topic,
    }));

void Report(string call, CallToolResult result) => Console.WriteLine(result.IsError == true
    ? $"{call}: error: {result.Content.OfType<TextContentBlock>().Single().Text}"
    : $"{call}: {result.StructuredContent!.Value.GetRawText()}");

// The curl tool runs curl's own arguments. With an mqtt: URL and no -d, it subscribes to the
// topic in the path; with -d, it publishes. -s keeps the progress meter out of the answer.
async Task Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = arguments.Prepend("-s").ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement output = answer.RootElement.GetProperty("standardOutput").GetProperty("content");
    Console.WriteLine($"curl exit {answer.RootElement.GetProperty("exitCode").GetInt32()}, output {output.GetRawText()}");
}
