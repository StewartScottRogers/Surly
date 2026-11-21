// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tells a missing file from an empty one: the status from curl-mcp's transfer tool,
// and the exit code from its curl tool with --fail, against a server surl-mcp started.
// Run: dotnet run missing-or-empty.cs -- <tool> <path>
//   tool   transfer (the default) or curl
//   path   empty.txt (the default), which the server has with no bytes in it, or missing.txt
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string tool = args.Length > 0 ? args[0] : "transfer";
string path = args.Length > 1 ? args[1] : "empty.txt";

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

// A server on a free port holding one file with nothing in it.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["empty.txt"] = "" },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

CallToolResult result;
JsonElement answer;
if (tool == "curl")
{
    // curl -f -s -w "%{http_code}" URL: the exit code says it, as in a script.
    result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-f", "-s", "-w", "%{http_code}", url + path },
    });
    answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode: {answer.GetProperty("exitCode").GetRawText()}");
    Console.WriteLine($"standardOutput: {answer.GetProperty("standardOutput").GetProperty("content").GetString()}");
}
else
{
    // transfer: the status says it; an empty body looks the same either way.
    result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
    answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"statusCode: {answer.GetProperty("statusCode").GetRawText()}");
    Console.WriteLine($"body: {answer.GetProperty("body").GetProperty("content").GetRawText()}");
}

JsonElement failure = answer.GetProperty("failure");
Console.WriteLine($"failure: {(failure.ValueKind == JsonValueKind.Null ? "null" : failure.GetProperty("kind").GetString())}");
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
