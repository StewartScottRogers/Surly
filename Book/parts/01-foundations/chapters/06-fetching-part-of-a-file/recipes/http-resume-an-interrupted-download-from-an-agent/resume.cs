// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent finds a partial download and resumes it with curl -C - through curl-mcp, then checks
// the file against what surl-mcp holds.
// Run: dotnet run resume.cs -- [mode [file]]
//   mode   resume (the default): curl -C -
//          if-range-old: the same, with an If-Range header holding an old date
//   file   the partial file, inside the folder both hosts are confined to (dl/records.txt by default)
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "resume";
string path = args.Length > 1 ? args[1] : "dl/records.txt";

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

// surl-mcp: records.txt is 300 bytes, thirty lines of ten, held in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["records.txt"] = string.Concat(Enumerable.Range(0, 30).Select(i => $"record {i:00}\n")) },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()! + "records.txt";

Console.WriteLine($"partial: {(File.Exists(path) ? new FileInfo(path).Length : 0)} bytes");

// curl-mcp: curl -C - works out the offset from the file, as on the console.
var curlArgs = new List<string> { "-s", "-C", "-" };
if (mode == "if-range-old")
{
    curlArgs.AddRange(["-H", "If-Range: Wed, 01 Jan 2020 00:00:00 GMT"]);
}

curlArgs.AddRange(["-o", path, "-w", "%{http_code} %{size_download}", url]);
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs.ToArray() });
JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode").GetRawText()}");
Console.WriteLine($"standardOutput: {answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}");

// surl-mcp's get_exchanges: the request as the server received it.
JsonElement exchange = (await RecordedAsync(EndsWithReply)).GetProperty("exchanges").EnumerateArray().Last();
string head = exchange.GetProperty("chunks").EnumerateArray()
    .First(chunk => chunk.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!;
string range = head.Split("\r\n").FirstOrDefault(line => line.StartsWith("Range:", StringComparison.Ordinal)) ?? "Range: (none)";
Console.WriteLine($"request: {range}");

// surl-mcp's read_file: what the server holds, against the file on disk.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/records.txt" });
string held = read.StructuredContent!.Value.GetProperty("text").GetString()!;
string joined = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path));
bool same = joined == held;
Console.WriteLine($"file: {joined.Length} bytes, identical to read_file: {(same ? "yes" : "no")}");
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return same && result.IsError != true ? 0 : 1;

// get_exchanges, asked again until the record holds what ready looks for and two answers 100 ms apart
// agree: the server's record can trail curl by a moment. At most 10 s, then the last answer.
async Task<JsonElement> RecordedAsync(Func<JsonElement, bool> ready, int after = 0)
{
    JsonElement last = default;
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId, ["after_exchange_id"] = after });
        JsonElement answer = result.StructuredContent!.Value.Clone();
        if (ready(answer) && last.ValueKind != JsonValueKind.Undefined && answer.GetRawText() == last.GetRawText())
        {
            return answer;
        }

        last = answer;
    }

    return last;
}

// True once the newest exchange ends with something the server sent, as every finished exchange here does.
static bool EndsWithReply(JsonElement answer) =>
    answer.GetProperty("exchanges").EnumerateArray().LastOrDefault() is { ValueKind: JsonValueKind.Object } newest
    && newest.GetProperty("chunks").EnumerateArray().LastOrDefault() is { ValueKind: JsonValueKind.Object } final
    && final.GetProperty("direction").GetString() == "sent";
