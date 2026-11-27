// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reads one slice of a file with curl-mcp, from a server surl-mcp started.
// Run: dotnet run read-slice.cs -- <tool> <first-last>
//   tool    transfer (the default), with a Range header; or curl, with -f -r
//   range   the bytes asked for, first and last counted from zero (100-199 by default)
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string tool = args.Length > 0 ? args[0] : "transfer";
string range = args.Length > 1 ? args[1] : "100-199";

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

// records.txt is 300 bytes, thirty lines of ten, so byte 100 starts "record 10".
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["records.txt"] = string.Concat(Enumerable.Range(0, 30).Select(i => $"record {i:00}\n")) },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

CallToolResult result;
JsonElement answer;
if (tool == "curl")
{
    // curl -f -s -r RANGE -w "%{http_code}": a status of 400 or more makes curl exit 22.
    result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-f", "-s", "-r", range, "-w", "%{http_code}", url + "records.txt" },
    });
    answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode: {answer.GetProperty("exitCode").GetRawText()}");
    Console.WriteLine($"standardOutput: {answer.GetProperty("standardOutput").GetProperty("content").GetString()}");
}
else
{
    // transfer: the range goes in a Range header; the answer is text, as JSON.
    var headers = new JsonArray(new JsonObject { ["name"] = "Range", ["value"] = $"bytes={range}" });
    result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "records.txt", ["headers"] = headers });
    answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement contentRange = answer.GetProperty("headers").EnumerateArray()
        .FirstOrDefault(h => h.GetProperty("name").GetString()!.Equals("Content-Range", StringComparison.OrdinalIgnoreCase));
    JsonElement body = answer.GetProperty("body");
    Console.WriteLine($"statusCode: {answer.GetProperty("statusCode").GetRawText()}");
    Console.WriteLine($"Content-Range: {(contentRange.ValueKind == JsonValueKind.Undefined ? "(none)" : contentRange.GetProperty("value").GetString())}");
    Console.WriteLine($"totalBytes: {body.GetProperty("totalBytes").GetRawText()}");
    Console.Write(body.GetProperty("content").GetString());
}

JsonElement failure = answer.GetProperty("failure");
Console.WriteLine($"failure: {(failure.ValueKind == JsonValueKind.Null ? "null" : failure.GetProperty("kind").GetString())}");
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
