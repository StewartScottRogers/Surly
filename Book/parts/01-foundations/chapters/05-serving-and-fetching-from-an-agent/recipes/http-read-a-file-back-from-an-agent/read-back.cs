// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches a file with curl-mcp's transfer, reads the same file from surl-mcp's store with
// read_file, and compares the bytes and the content type.
// Run: dotnet run read-back.cs [-- path]   (default /hello.txt; try /logo.png, /docs, /.env or /nope.txt)
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string path = args.Length > 0 ? args[0] : "/hello.txt";

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

// surl-mcp: a text file, a binary file given as base64, a dot file and a file in a folder.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string>
    {
        ["hello.txt"] = "hello from surl\n",
        [".env"] = "SECRET=1\n",
        ["docs/guide.md"] = "# Guide\n",
    },
    ["files_base64"] = new Dictionary<string, string> { ["logo.png"] = "iVBORw0KGgoAAP8A/w==" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!.TrimEnd('/');

// What a client gets: curl-mcp's transfer, answered in text, as JSON.
CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"transfer: {answer.GetProperty("statusCode")}");

// What the server holds: surl-mcp's read_file, answered in structured content.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = path });
if (read.IsError == true)
{
    Console.WriteLine($"read_file: {read.Content.OfType<TextContentBlock>().Single().Text}");
    Console.WriteLine("tool error: yes");
    return 1;
}

JsonElement file = read.StructuredContent!.Value;
bool isText = file.TryGetProperty("text", out JsonElement text);
byte[] held = isText ? Encoding.UTF8.GetBytes(text.GetString()!) : Convert.FromBase64String(file.GetProperty("base64").GetString()!);
Console.WriteLine($"read_file: {file.GetProperty("byte_count")} bytes as {(isText ? "text" : "base64")}, content_type {file.GetProperty("content_type")}");

// Compare: the body's bytes, and the Content-Type header, with what read_file answered.
JsonElement body = answer.GetProperty("body");
byte[] got = body.GetProperty("isBase64").GetBoolean()
    ? Convert.FromBase64String(body.GetProperty("content").GetString()!)
    : Encoding.UTF8.GetBytes(body.GetProperty("content").GetString()!);
string type = answer.GetProperty("headers").EnumerateArray()
    .First(h => h.GetProperty("name").GetString()!.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
    .GetProperty("value").GetString()!;
Console.WriteLine($"same bytes: {(got.AsSpan().SequenceEqual(held) ? "yes" : "no")}");
Console.WriteLine($"same type: {(type == file.GetProperty("content_type").GetString() ? "yes" : "no")} ({type})");
Console.WriteLine("tool error: no");
return 0;
