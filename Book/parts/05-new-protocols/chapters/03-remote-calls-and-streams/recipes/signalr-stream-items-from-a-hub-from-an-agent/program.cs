// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a SignalR hub with surl-mcp and streams items from it with curl-mcp, over the official MCP SDK.
// Run: dotnet run program.cs -- <case>
//   stream          stream Count with 5, then ask the hub what it saw (the default)
//   empty           stream Count with 0
//   unknown-method  stream a method the hub does not have
//   wrong-type      invoke Count as an ordinary call, then stream Echo
//   invalid-arg     stream Count with -1
//   refused         stream from a hub that allows no anonymous client
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "stream";

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

// A hub on a free port. --allow-anonymous is surl's own option, passed in arguments; refused leaves it out.
var startArguments = new Dictionary<string, object?> { ["listen_urls"] = new[] { "signalr://127.0.0.1:0" } };
if (which != "refused")
{
    startArguments["arguments"] = new[] { "--allow-anonymous" };
}

CallToolResult started = await surl.CallToolAsync("start_server", startArguments);
JsonElement hub = started.StructuredContent!.Value;
string server = hub.GetProperty("server_id").GetString()!;
string url = hub.GetProperty("bound_urls")[0].GetString()!;
if (which == "stream")
{
    // The warning surl prints for the loosening comes back with the start.
    Console.WriteLine(hub.GetProperty("log_lines")[0].GetString());
}

bool failed = false;
switch (which)
{
    case "empty":
        await Call(4, "Count", "[0]");
        break;
    case "unknown-method":
        await Call(4, "Shout", "[]");
        break;
    case "wrong-type":
        await Call(1, "Count", "[3]");
        await Call(4, "Echo", "[\"hi\"]");
        break;
    case "invalid-arg":
        await Call(4, "Count", "[-1]");
        break;
    default:
        await Call(4, "Count", "[5]");
        break;
}

if (which == "stream")
{
    // What the hub saw: one exchange for the negotiate request and one for the WebSocket that carried the stream.
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "exchange_started" },
    });
    JsonElement.ArrayEnumerator started2 = events.StructuredContent!.Value.GetProperty("events").EnumerateArray();
    Console.WriteLine($"hub saw {started2.Count()} exchanges, scheme {started2.First().GetProperty("scheme")}");
}

return failed ? 1 : 0;

// The curl tool runs curl's own arguments: -d is the one message sent after the handshake, a stream
// invocation (type 4) or an ordinary one (type 1); -s keeps the progress meter out of the answer.
async Task Call(int type, string target, string arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-d", $$"""{"type":{{type}},"target":"{{target}}","arguments":{{arguments}}}""", url },
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    Console.WriteLine($"{target}: exit {answer.GetProperty("exitCode")}, output "
        + answer.GetProperty("standardOutput").GetProperty("content").GetRawText()
        + (error == "" ? "" : $", error {error}") + $", tool error {(result.IsError == true ? "yes" : "no")}");
    failed |= result.IsError == true;
}
