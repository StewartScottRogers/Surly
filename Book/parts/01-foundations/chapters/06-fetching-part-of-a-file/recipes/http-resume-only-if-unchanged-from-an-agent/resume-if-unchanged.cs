// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent downloads a file in two halves with curl-mcp, replaces it with surl-mcp's put_file in
// between, and resumes with If-Range, so a changed file comes back whole instead of being spliced.
// Run: dotnet run resume-if-unchanged.cs [-- <case>]
//   (none)       the file is replaced between the halves; the resume sends the ETag
//   unchanged    the file is left alone; the resume sends the ETag
//   no-if-range  the file is replaced; the resume sends Range alone
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "changed";
string v1 = string.Concat(Enumerable.Range(0, 10).Select(i => $"version 1 line {i:00}\n"));
string v2 = string.Concat(Enumerable.Range(0, 10).Select(i => $"version 2 line {i:00}\n"));

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

// The server holds version 1 of records.txt: 180 bytes, ten lines of eighteen.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["records.txt"] = v1 },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "records.txt";

// The first half: bytes 0 to 99, standing in for a download that was interrupted. Keep the ETag.
(int status, string got, string? etag) = await Transfer(("Range", "bytes=0-99"));
Console.WriteLine($"first half: {status}, {got.Length} bytes, ETag {(etag is null ? "missing" : "kept")}");
string local = got;

// The agent replaces the file between the halves.
if (which != "unchanged")
{
    CallToolResult put = await surl.CallToolAsync("put_file", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["path"] = "/records.txt", ["text"] = v2,
    });
    Console.WriteLine($"put_file: {put.StructuredContent!.Value.GetProperty("byte_count")} bytes");
}

// The second half: the rest, but only if the file is still the version the first half came from.
(int second, string rest, _) = which == "no-if-range"
    ? await Transfer(("Range", $"bytes={local.Length}-"))
    : await Transfer(("Range", $"bytes={local.Length}-"), ("If-Range", etag!));
local = second switch
{
    206 => local + rest,   // the same version: append
    200 => rest,           // a different version: the whole of it replaces the first half
    _ => throw new InvalidOperationException($"unexpected {second}"),
};
Console.WriteLine($"second half: {second}, {rest.Length} bytes");
Console.WriteLine($"result: {(local == v1 ? "version 1" : local == v2 ? "version 2" : "neither version")}, {local.Length} bytes");

// get_exchanges: each transfer is its own exchange; the last one's request head is the resume.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
JsonElement last = recorded.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last();
string head = last.GetProperty("chunks").EnumerateArray().First(c => c.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!;
foreach (string line in head.Split("\r\n").Where(l => l.StartsWith("Range:") || l.StartsWith("If-Range:")))
{
    Console.WriteLine($"server saw: {(line == $"If-Range: {etag}" ? "If-Range: <the ETag of the first half>" : line)}");
}
return 0;

// curl-mcp transfer with extra headers; its answer is text, as JSON.
async Task<(int Status, string Body, string? ETag)> Transfer(params (string Name, string Value)[] headers)
{
    var list = new JsonArray();
    foreach ((string name, string value) in headers)
    {
        list.Add((JsonNode)new JsonObject { ["name"] = name, ["value"] = value });
    }
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url, ["headers"] = list, ["timeoutSeconds"] = 30 });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string? tag = answer.GetProperty("headers").EnumerateArray()
        .Where(h => h.GetProperty("name").GetString()!.Equals("ETag", StringComparison.OrdinalIgnoreCase))
        .Select(h => h.GetProperty("value").GetString()).FirstOrDefault();
    return (answer.GetProperty("statusCode").GetInt32(), answer.GetProperty("body").GetProperty("content").GetString()!, tag);
}
