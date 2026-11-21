// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a surl-mcp TFTP server and uploads to it with curl-mcp's curl tool, over the official MCP SDK.
// Run: dotnet run upload.cs [-- SCENARIO]
//   stored (the default), read-back, overwrite, several-blocks, several-blocks-read-back, no-subfolders,
//   no-dot-files, uploads-off, uploads-off-error-two, uploads-off-nothing-kept, over-the-limit, over-the-limit-kept-nothing
#:package ModelContextProtocol.Core@2.2.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "stored";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The local files to send lie in it.
string here = Directory.GetCurrentDirectory();
File.WriteAllText("r1.cfg", "hostname r1\n");
File.WriteAllText("r2.cfg", "hostname r2\n");
File.WriteAllText("big.bin", new string('a', 1300));
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

// A TFTP server on a free UDP port. Its words are surl's own: uploads on, except for the uploads-off scenarios,
// and a size limit for the over-the-limit ones.
string[] words = scenario switch
{
    var s when s.StartsWith("uploads-off") => [],
    var s when s.StartsWith("over-the-limit") => ["--allow-uploads", "--max-filesize", "1000"],
    _ => ["--allow-uploads"],
};
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "tftp://127.0.0.1:0" },
    ["arguments"] = words,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string root = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl -T: the answer is the exit code, then curl's message when it failed.
async Task<string> Put(string local, string name)
{
    JsonElement answer = await Curl(["-s", "-S", "-T", local, root + name]);
    int code = answer.GetProperty("exitCode").GetInt32();
    return code == 0 ? "exit code 0" : $"exit code {code}, {answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}";
}

// What the server holds under a name, read from its store with surl-mcp's read_file.
async Task<string> Stored(string name)
{
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/" + name });
    return read.IsError == true ? "(nothing)" : read.StructuredContent!.Value.GetProperty("text").GetString()!;
}

switch (scenario)
{
    case "stored":
        Console.WriteLine(await Put("r1.cfg", "router.cfg"));
        Console.Write($"stored: {await Stored("router.cfg")}");
        break;
    case "read-back":
        await Put("r1.cfg", "router.cfg");
        // transfer reads a URL; a TFTP answer has no HTTP status, so statusCode is 0.
        CallToolResult read = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = root + "router.cfg" });
        JsonElement body = JsonDocument.Parse(read.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        Console.Write($"read: {body.GetProperty("body").GetProperty("content").GetString()}");
        break;
    case "overwrite":
        await Put("r1.cfg", "router.cfg");
        await Put("r2.cfg", "router.cfg");
        Console.Write($"stored: {await Stored("router.cfg")}");
        break;
    case "several-blocks":
        Console.WriteLine(await Put("big.bin", "big.bin"));
        Console.WriteLine($"stored: {(await Stored("big.bin")).Length} bytes");
        break;
    case "several-blocks-read-back":
        await Put("big.bin", "big.bin");
        await Curl(["-s", "-S", "-o", "copy.bin", root + "big.bin"]);
        Console.WriteLine($"read: {new FileInfo("copy.bin").Length} bytes");
        break;
    case "no-subfolders": Console.WriteLine(await Put("r1.cfg", "configs/r1.cfg")); break;
    case "no-dot-files": Console.WriteLine(await Put("r1.cfg", ".hidden")); break;
    case "uploads-off": Console.WriteLine(await Put("r1.cfg", "router.cfg")); break;
    case "uploads-off-error-two":
        await Put("r1.cfg", "router.cfg");
        // get_exchanges: each chunk is one datagram, base64. An error packet is opcode 5, then a two-byte error code.
        CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
        var packets = recorded.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
            .Where(c => c.GetProperty("direction").GetString() == "sent")
            .Select(c => Convert.FromBase64String(c.GetProperty("data").GetString()!));
        byte[] error = packets.Single(p => p[1] == 5);
        Console.WriteLine($"server sent: opcode {error[1]}, error {error[2] * 256 + error[3]}");
        break;
    case "uploads-off-nothing-kept":
        await Put("r1.cfg", "router.cfg");
        Console.WriteLine($"stored: {await Stored("router.cfg")}");
        break;
    case "over-the-limit": Console.WriteLine(await Put("big.bin", "big.bin")); break;
    case "over-the-limit-kept-nothing":
        await Put("big.bin", "big.bin");
        Console.WriteLine($"stored: {await Stored("big.bin")}");
        break;
}
return 0;

// curl-mcp's curl tool: its answer is JSON with exitCode, standardOutput and standardError.
async Task<JsonElement> Curl(string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}
