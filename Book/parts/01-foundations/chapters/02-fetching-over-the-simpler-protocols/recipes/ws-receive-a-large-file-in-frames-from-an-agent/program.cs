// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent receives files bigger than 64 KiB over WebSocket with curl-mcp, compares each with the original,
// and reads from surl-mcp's exchange record how the server framed it. Over the official MCP SDK.
// Run: dotnet run program.cs
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// surl-mcp: a WebSocket server on a free port, three files of random bytes in memory, sent as base64.
byte[] big = new byte[200_000];
new Random(7).NextBytes(big);
var files = new Dictionary<string, byte[]>
{
    ["big.bin"] = big,
    ["at-limit.bin"] = big[..65_536],
    ["over-limit.bin"] = big[..65_537],
};
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ws://127.0.0.1:0" },
    ["files_base64"] = files.ToDictionary(file => file.Key, file => Convert.ToBase64String(file.Value)),
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp: curl writes the message it receives to the file named by -o, in its folder. The program reads
// that file and compares it with the original.
CallToolResult saved = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-o", "got.bin", url + "big.bin" },
});
JsonElement exit = JsonDocument.Parse(saved.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("exitCode");
bool same = File.ReadAllBytes("got.bin").AsSpan().SequenceEqual(big);
Console.WriteLine($"big.bin: curl exit {exit}, identical: {(same ? "yes" : "no")}");

// curl-mcp's transfer tool answers the status, the body and what curl counted. sizeDownload counts the bytes
// that crossed, frame headers and the server's close included, so what it has over the file is the framing.
foreach ((string name, byte[] original) in files)
{
    CallToolResult transferred = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + name });
    JsonElement answer = JsonDocument.Parse(transferred.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement body = answer.GetProperty("body");
    long counted = answer.GetProperty("info").GetProperty("sizeDownload").GetInt64();
    byte[] message = Convert.FromBase64String(body.GetProperty("content").GetString()!);
    Console.WriteLine($"{name}: transfer {answer.GetProperty("statusCode")}, isBase64 {(body.GetProperty("isBase64").GetBoolean() ? "true" : "false")}, " +
        $"{message.Length} bytes, identical: {(message.AsSpan().SequenceEqual(original) ? "yes" : "no")}, framing {counted - message.Length}");
}

// surl-mcp: each exchange as the server saw it, the request head, the 101 head and the bytes it sent, as
// base64. The record shows the first 64 KiB only, so it holds the first frame's header and no more.
var seen = new HashSet<string>();
CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
foreach (JsonElement exchange in exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    JsonElement chunks = exchange.GetProperty("chunks");
    string request = chunks.EnumerateArray().First(c => c.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!;
    string name = request.Split(' ')[1].TrimStart('/');
    if (!seen.Add(name))
    {
        continue; // big.bin was fetched twice; its first exchange is shown
    }

    JsonElement sent = chunks.EnumerateArray().First(c => c.GetProperty("encoding").GetString() == "base64");
    byte[] frame = Convert.FromBase64String(sent.GetProperty("data").GetString()!);
    ulong length = 0;
    foreach (byte b in frame[2..10]) { length = (length << 8) | b; }
    Console.WriteLine($"{name}: first frame 0x{frame[0]:x2} 0x{frame[1]:x2}, final: {((frame[0] & 0x80) != 0 ? "yes" : "no")}, length {length}");
    if (name == "big.bin")
    {
        Console.WriteLine($"{name}: recorded more than the file: {(exchange.GetProperty("recorded_bytes").GetInt64() > 200_000 ? "yes" : "no")}, " +
            $"shown {exchange.GetProperty("shown_bytes")}, cut: {exchange.GetProperty("is_cut")}");
    }
}

return 0;
