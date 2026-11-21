// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent receives a file over WebSocket with curl-mcp's curl tool and compares it with what
// surl-mcp's read_file says the server holds.
// Run: dotnet run program.cs -- <case>
//   text      hello.txt, text (the default)
//   binary    logo.bin, six bytes that are not text
//   frame     hello.txt, then the first frame the server sent, from get_exchanges
//   missing   a path the server does not have
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "text";

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

// surl-mcp: a WebSocket server on a free port, a text file and a binary one in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ws://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    ["files_base64"] = new Dictionary<string, string> { ["logo.bin"] = "AAEC/f7/" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string path = which switch { "binary" => "/logo.bin", "missing" => "/missing.txt", _ => "/hello.txt" };

// curl-mcp: curl ws://... writes the message it receives to standard output. It answers in text, as JSON.
CallToolResult received = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", url + path.TrimStart('/') },
});
JsonElement answer = JsonDocument.Parse(received.Content.OfType<TextContentBlock>().Single().Text).RootElement;
JsonElement output = answer.GetProperty("standardOutput");
bool outputIsBase64 = output.GetProperty("isBase64").GetBoolean();
string message = output.GetProperty("content").GetString()!;
Console.WriteLine($"curl: exitCode {answer.GetProperty("exitCode")}, tool error {(received.IsError == true ? "yes" : "no")}, {output.GetProperty("totalBytes")} bytes, isBase64 {(outputIsBase64 ? "true" : "false")}");

// surl-mcp: the file as the server holds it, read from its store rather than over the network.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = path });
if (read.IsError == true)
{
    Console.WriteLine($"read_file: {read.Content.OfType<TextContentBlock>().Single().Text}");
    return 1;
}

// read_file answers text for valid UTF-8 and base64 otherwise; compare like with like.
JsonElement file = read.StructuredContent!.Value;
bool fileIsText = file.TryGetProperty("text", out JsonElement text);
string held = fileIsText ? text.GetString()! : file.GetProperty("base64").GetString()!;
Console.WriteLine($"read_file: {file.GetProperty("byte_count")} bytes, as {(fileIsText ? "text" : "base64")}");
Console.WriteLine(message == held && outputIsBase64 != fileIsText ? "the same bytes" : "different bytes");

if (which == "frame")
{
    // The exchange as the server saw it: the request head, the 101 head, then WebSocket frames in base64.
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
    JsonElement chunks = exchanges.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks");
    JsonElement first = chunks.EnumerateArray().First(c => c.GetProperty("encoding").GetString() == "base64");
    byte[] frame = Convert.FromBase64String(first.GetProperty("data").GetString()!);
    Console.WriteLine($"first frame: 0x{frame[0]:x2} 0x{frame[1]:x2}, then {frame.Length - 2} bytes");
}

return 0;
