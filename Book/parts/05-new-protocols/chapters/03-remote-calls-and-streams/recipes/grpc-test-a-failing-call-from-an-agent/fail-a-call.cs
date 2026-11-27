// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a gRPC server fail calls with chosen statuses: surl-mcp starts the server, curl-mcp calls
// it and reports the status. Run: dotnet run fail-a-call.cs -- <case>
//   solution      a call that asks for NOT_FOUND with a message
//   statuses      INVALID_ARGUMENT, and UNAVAILABLE with no message
//   zero          status 0, which is a success
//   message       a message with a percent sign, and a status number that is not one
//   part-way      a stream that fails after its replies
//   deadlines     a status the server chooses, and a client that stops waiting
#:package ModelContextProtocol.Core@2.4.0

using System.Text.RegularExpressions;
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

// A gRPC server on a free port. It serves surl.test.Echo, and takes anonymous callers.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "grpc://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "surl.test.Echo/";

switch (which)
{
    case "solution":
        await Call("Echo", """{"message":"order 7","statusCode":5,"statusMessage":"no such order"}""");
        break;
    case "statuses":
        await Call("Echo", """{"statusCode":3,"statusMessage":"quantity must be positive"}""");
        await Call("Echo", """{"statusCode":14}""");
        break;
    case "zero":
        await Call("Echo", """{"message":"order 7","statusCode":0}""");
        break;
    case "message":
        await Call("Echo", """{"statusCode":9,"statusMessage":"disk 100% full"}""");
        await Call("Echo", """{"statusCode":99,"statusMessage":"odd"}""");
        break;
    case "part-way":
        await Call("Expand", """{"message":"s","count":3,"statusCode":13,"statusMessage":"boom"}""");
        break;
    case "deadlines":
        await Call("Echo", """{"statusCode":4}""");
        // -m also goes to the server as grpc-timeout, and surl ends the call DEADLINE_EXCEEDED when it passes,
        // in a race with curl's own timer. An empty -H grpc-timeout: keeps it off the wire, so only curl stops.
        await Call("Echo", """{"message":"x","delayMilliseconds":3000}""", "-m", "1", "-H", "grpc-timeout:");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// The curl tool runs curl's own arguments, as the console would; the request is JSON given with -d, and the
// types come from the server's reflection. -sS keeps the progress meter out and the error message in. A
// call that ends in a status other than OK, or that curl gives up on, is a tool error with curl's exit code.
async Task Call(string method, string request, params string[] more)
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
    // The status is on the error's first line: "curl: (22) grpc-status 5 (NOT_FOUND): no such order".
    Match status = Regex.Match(error, @"grpc-status.*");
    string said = status.Success ? status.Value.Trim() : exit == 28 ? "timed out" : error;
    Console.WriteLine($"curl exit {exit}, tool error {(result.IsError == true ? "yes" : "no")}, output [{output}]{(said == "" ? "" : $", {said}")}");
}
