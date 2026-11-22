// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent gives gRPC calls a time limit: surl-mcp starts a server that can hold a reply back, curl-mcp calls it
// with -m and the program reads the tool's answer. Run: dotnet run time-out.cs -- MODE
//   gives-up         a call held back 3 s, a limit of 1 s
//   within-limit     a call held back 200 ms under a limit of 10 s
//   how-long         how long the tool call took
//   server-deadline  a grpc-timeout header of 300m
//   stream-deadline  a stream cut by a 1S grpc-timeout
//   either           -m 1 with nothing to keep it on the client
#:package ModelContextProtocol.Core@*

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "gives-up";

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

// A gRPC server on a free port. It serves surl.test.Echo, and takes anonymous callers.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "grpc://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
});
if (started.IsError == true)
{
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    return 3;
}
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "surl.test.Echo/";

// -m also goes to the server as grpc-timeout; an empty -H grpc-timeout: keeps it off the wire, so only curl stops.
string[] noHeader = ["-H", "grpc-timeout:"];

switch (which)
{
    case "gives-up":
        Console.WriteLine((await Call("Echo", """{"message":"x","delayMilliseconds":3000}""", ["-m", "1", .. noHeader])).Line);
        break;
    case "within-limit":
        Console.WriteLine((await Call("Echo", """{"message":"x","delayMilliseconds":200}""", ["-m", "10"])).Line);
        break;
    case "how-long":
    {
        // The first call in a process is not a fair measure of anything; make one before timing.
        await Call("Echo", """{"message":"warm-up"}""", []);
        var clock = Stopwatch.StartNew();
        await Call("Echo", """{"message":"x","delayMilliseconds":3000}""", ["-m", "1", .. noHeader]);
        Console.WriteLine(clock.Elapsed.TotalSeconds is >= 1 and < 3 ? "the tool call took between 1 and 3 s" : $"the tool call took {clock.Elapsed.TotalSeconds:F1} s");
        break;
    }
    case "server-deadline":
        Console.WriteLine((await Call("Echo", """{"message":"x","delayMilliseconds":3000}""", ["-m", "10", "-H", "grpc-timeout: 300m"])).Line);
        break;
    case "stream-deadline":
    {
        var (line, replies, exit) = await Call("Expand", """{"message":"s","count":10,"delayMilliseconds":300}""", ["-m", "10", "-H", "grpc-timeout: 1S"]);
        Console.WriteLine($"{(replies is > 0 and < 10 ? "some" : replies.ToString())} of 10 replies, curl exit {exit}, {Regex.Match(line, "grpc-status.*").Value}");
        break;
    }
    case "either":
    {
        // curl also sends -m to the server as grpc-timeout, so the two timers race: curl gives up, exit 28, or the
        // server ends the call DEADLINE_EXCEEDED, exit 22. Which comes first is not for a test to depend on.
        var (line, _, exit) = await Call("Echo", """{"message":"x","delayMilliseconds":3000}""", ["-m", "1"]);
        Console.WriteLine(exit switch
        {
            28 => "ended at the limit: curl timed out",
            22 when line.Contains("DEADLINE_EXCEEDED") => "ended at the limit: the server's deadline",
            _ => $"did not end at the limit: {line}",
        });
        break;
    }
    default:
        Console.Error.WriteLine($"unknown mode {which}");
        return 2;
}

return 0;

// The curl tool runs curl's own arguments, as the console would; the request is JSON given with -d, and the
// types come from the server's reflection. A call curl gives up on, or that ends in a status other than OK, is a
// tool error with curl's exit code; the answer carries the output and the error text.
async Task<(string Line, int Replies, int Exit)> Call(string method, string request, string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS" }.Concat(more).Concat(new[] { "-d", request, url + method }).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    int exit = root.GetProperty("exitCode").GetInt32();
    string output = Regex.Replace(root.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim(), @"\s+", " ");
    string error = root.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    Match status = Regex.Match(error, @"grpc-status.*");
    string said = status.Success ? status.Value.Trim() : exit == 28 ? "timed out" : "";
    string line = $"curl exit {exit}, tool error {(result.IsError == true ? "yes" : "no")}, output [{output}]{(said == "" ? "" : $", {said}")}";
    return (line, Regex.Matches(output, "\"message\"").Count, exit);
}
