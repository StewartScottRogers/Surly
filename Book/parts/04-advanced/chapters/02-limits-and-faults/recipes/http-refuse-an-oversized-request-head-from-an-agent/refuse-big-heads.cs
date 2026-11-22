// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a server with a 1 KiB request-head limit through surl-mcp and fetches from it
// through curl-mcp with a long cookie. Run: dotnet run refuse-big-heads.cs -- <case>
//   big        a 1,200-byte cookie under the 1 KiB limit (the default)
//   small      a 600-byte cookie under the same limit
//   default    the 1,200-byte cookie, with surl's own limit left in place
//   recorded   the 1,200-byte cookie, then what the server says it sent back
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "big";

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

// start_server takes surl's options in arguments, as surl's own words.
string[] arguments = which == "default" ? [] : ["--max-request-head", "1k"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// transfer takes extra headers as name and value pairs; it answers in text, as JSON.
int length = which == "small" ? 600 : 1200;
CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?>
{
    ["url"] = url + "hello.txt",
    ["headers"] = new JsonArray { new JsonObject { ["name"] = "Cookie", ["value"] = "session=" + new string('x', length) } },
});
using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
JsonElement root = answer.RootElement;
JsonElement body = root.GetProperty("body");
Console.WriteLine($"statusCode: {root.GetProperty("statusCode")}");
Console.WriteLine($"failure: {root.GetProperty("failure").GetRawText()}");
if (root.GetProperty("statusCode").GetInt32() == 200)
{
    Console.Write(body.GetProperty("content").GetString());
}
else
{
    string connection = root.GetProperty("headers").EnumerateArray()
        .Where(h => h.GetProperty("name").GetString() == "Connection")
        .Select(h => h.GetProperty("value").GetString()).FirstOrDefault() ?? "(none)";
    Console.WriteLine($"body: {body.GetProperty("totalBytes")} bytes, Connection: {connection}");
}

if (which == "recorded")
{
    // get_exchanges shows what the server received and sent, byte for byte; the last exchange is the fetch.
    // The record can trail curl's answer by a moment: ask again until the answer is in it, for at most 10 s.
    string sent = "";
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); ; await Task.Delay(100))
    {
        CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
        JsonElement last = exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Last();
        sent = string.Concat(last.GetProperty("chunks").EnumerateArray()
            .Where(c => c.GetProperty("direction").GetString() == "sent")
            .Select(c => c.GetProperty("data").GetString()));
        if (sent.Contains("\r\n") || DateTime.UtcNow >= deadline) break;
    }
    Console.WriteLine($"get_exchanges: sent {sent.Split("\r\n")[0]}");
}

Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
