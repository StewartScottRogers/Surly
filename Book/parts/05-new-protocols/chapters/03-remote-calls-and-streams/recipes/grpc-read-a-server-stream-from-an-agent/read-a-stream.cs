// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reads a server-streaming gRPC method: surl-mcp starts the server, curl-mcp calls Expand and the program
// reads the replies from the tool's answer. Run: dotnet run read-a-stream.cs -- MODE
//   stream      Expand with a count of 3
//   empty       a count of 0, and no count
//   limit       a count above 1000
//   arrival     each reply held back 300 ms, read for one second
//   part-way    a stream that ends in a status after its replies
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
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

// A gRPC server on a free port. It serves surl.test.Echo, and takes anonymous callers.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "grpc://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
});
string expand = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "surl.test.Echo/Expand";

switch (which)
{
    case "stream":
        Console.WriteLine((await Call("""{"message":"s","count":3}""")).Line);
        break;
    case "empty":
        Console.WriteLine((await Call("""{"message":"s","count":0}""")).Line);
        Console.WriteLine((await Call("""{"message":"s"}""")).Line);
        break;
    case "limit":
        Console.WriteLine((await Call("""{"message":"s","count":1001}""")).Line);
        break;
    case "part-way":
        Console.WriteLine((await Call("""{"message":"s","count":3,"statusCode":13,"statusMessage":"boom"}""")).Line);
        break;
    case "arrival":
    {
        // -m also goes to the server as grpc-timeout; an empty -H grpc-timeout: keeps it off, so only curl stops.
        var (line, replies, exit) = await Call("""{"message":"s","count":10,"delayMilliseconds":300}""", "-m", "1", "-H", "grpc-timeout:");
        Console.WriteLine($"curl exit {exit}, {(replies is > 0 and < 10 ? "some" : replies.ToString())} of 10 replies");
        break;
    }
    default:
        Console.Error.WriteLine($"unknown mode {which}");
        return 2;
}

return 0;

// The curl tool runs curl's own arguments, as the console would; the request is JSON given with -d, and the
// types come from the server's reflection. The answer carries the replies, the exit code and curl's error.
async Task<(string Line, int Replies, int Exit)> Call(string request, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS" }.Concat(more).Concat(new[] { "-d", request, expand }).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    int exit = root.GetProperty("exitCode").GetInt32();
    string output = Regex.Replace(root.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim(), @"\s+", " ");
    string error = root.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    Match status = Regex.Match(error, @"grpc-status.*");
    int replies = Regex.Matches(output, "\"message\"").Count;
    return ($"exit {exit}, output [{output}]{(status.Success ? ", " + status.Value.Trim() : "")}", replies, exit);
}
