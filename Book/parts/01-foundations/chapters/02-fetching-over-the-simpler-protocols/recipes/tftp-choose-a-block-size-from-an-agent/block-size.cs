// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a 3,000-byte file over TFTP with surl-mcp, fetches it with curl-mcp at a chosen block
// size, and reads the datagrams back with get_exchanges, over the official MCP SDK.
// Run: dotnet run block-size.cs [-- <case>]
//   fetch      block size 1024: what curl said, what the server saw, the data blocks (the default)
//   default    no --tftp-blksize
//   none       --tftp-no-options
//   large      a block size of 70000
//   missing    a file the server does not have
//   transfer   the transfer tool, which has no TFTP option
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fetch";

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

// surl-mcp: a TFTP server on a free UDP port. The file is binary, so it goes in files_base64.
byte[] image = RandomNumberGenerator.GetBytes(3000);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "tftp://127.0.0.1:0" },
    ["files_base64"] = new Dictionary<string, string> { ["big.bin"] = Convert.ToBase64String(image) },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string root = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

if (which == "transfer")
{
    // transfer takes no TFTP options: the block size is whatever curl asks for by default.
    CallToolResult transferred = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = root + "big.bin" });
    JsonElement answer = JsonDocument.Parse(transferred.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"statusCode: {answer.GetProperty("statusCode")}");
    Console.WriteLine($"bytes: {Convert.FromBase64String(answer.GetProperty("body").GetProperty("content").GetString()!).Length}");
    Console.WriteLine($"request: {Describe(await Datagram(0))}");
    return 0;
}

// curl-mcp: the curl tool with the block-size option the case calls for. -v puts curl's trace in standardError.
string[] options = which switch
{
    "default" => [],
    "none" => ["--tftp-no-options"],
    "large" => ["--tftp-blksize", "70000"],
    _ => ["--tftp-blksize", "1024"],
};
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-v", "-o", "copy.bin" }.Concat(options).Append(root + (which == "missing" ? "nope.bin" : "big.bin")).ToArray(),
});
JsonElement ran = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"exit: {ran.GetProperty("exitCode")}");
if (ran.GetProperty("exitCode").GetInt32() != 0)
{
    Console.WriteLine(ran.GetProperty("failure").GetProperty("text").GetString()!.Split('\n').Last());
}
else
{
    // What curl itself says it was answered with.
    string trace = ran.GetProperty("standardError").GetProperty("content").GetString()!;
    foreach (string line in trace.Split('\n').Where(l => l.Contains("parsed from OACK")))
    {
        Console.WriteLine($"curl: {line.TrimStart('*', ' ').TrimEnd('\r')}");
    }

    Console.WriteLine(File.ReadAllBytes("copy.bin").AsSpan().SequenceEqual(image) ? "copy: identical" : "copy: different");
}

// surl-mcp: what the server received and sent, datagram by datagram.
JsonElement chunks = await Chunks();
Console.WriteLine($"request: {Describe(Bytes(chunks[0]))}");
Console.WriteLine($"reply: {Describe(Bytes(chunks[1]))}");
Console.WriteLine($"data blocks: {chunks.EnumerateArray().Count(c => c.GetProperty("direction").GetString() == "sent" && Bytes(c)[1] == 3)}");
return 0;

async Task<JsonElement> Chunks()
{
    return (await RecordedAsync(answer => answer.GetProperty("exchanges").GetArrayLength() > 0)).GetProperty("exchanges")[0].GetProperty("chunks");
}

async Task<byte[]> Datagram(int index) => Bytes((await Chunks())[index]);

// Every TFTP exchange is binary, so each chunk is base64.
static byte[] Bytes(JsonElement chunk) => Convert.FromBase64String(chunk.GetProperty("data").GetString()!);

// The opcode, then the file name, mode and options of a request, the options of an option acknowledgement,
// the number of a data block or the number of an error.
static string Describe(byte[] datagram) => datagram[1] switch
{
    3 => $"opcode 3, block {datagram[2] * 256 + datagram[3]}",
    5 => $"opcode 5, error {datagram[2] * 256 + datagram[3]}",
    _ => $"opcode {datagram[1]}, {string.Join(" ", Encoding.ASCII.GetString(datagram[2..]).Split('\0', StringSplitOptions.RemoveEmptyEntries))}",
};

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
