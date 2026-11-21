// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent uploads a file over FTP with curl-mcp and proves every byte arrived: it reads the stored copy
// back from surl-mcp, downloads it again, compares SHA-256 hashes, and reads what the server received.
// Run: dotnet run check-upload.cs [-- <case>]
//   check      upload with --allow-uploads on, then compare the stored copy and a download with the original (the default)
//   damaged    the same, after the stored copy is replaced with one that differs by one byte
//   refused    upload to a server that was not told to accept uploads
//   too-big    upload to a server whose --max-filesize is smaller than the file
//   no-login   upload to a server that was not told to accept anonymous logins
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "check";

// The file to send: every byte value once, then two CR LF pairs, which a text-mode transfer would rewrite.
byte[] original = Enumerable.Range(0, 256).Select(i => (byte)i).Concat(new byte[] { 13, 10, 13, 10 }).ToArray();
File.WriteAllBytes("blob.bin", original);

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

// surl-mcp: an FTP server with no files. Uploads are --allow-uploads, and --allow-anonymous lets curl log in;
// both loosen surl, so each run names exactly what it needs, as surl's own words.
var surlArguments = new List<string>();
if (which != "no-login")
{
    surlArguments.Add("--allow-anonymous");
}

if (which != "refused")
{
    surlArguments.Add("--allow-uploads");
}

if (which == "too-big")
{
    surlArguments.AddRange(["--max-filesize", "100"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = surlArguments,
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "blob.bin";

// Step 1: upload. curl -T sends the local file; the answer carries curl's exit code.
JsonElement upload = await Curl(["-s", "-T", "blob.bin", url]);
Console.WriteLine($"upload exit: {upload.GetProperty("exitCode")}");

// Step 2: what the server holds. read_file answers base64, so every byte is readable; an error answer is "no file".
if (which == "damaged")
{
    byte[] changed = (byte[])original.Clone();
    changed[100] ^= 1;
    await surl.CallToolAsync("put_file", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["path"] = "/blob.bin", ["base64"] = Convert.ToBase64String(changed),
    });
}

CallToolResult stored = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/blob.bin" });
if (stored.IsError == true)
{
    Console.WriteLine("stored: no file");
    return 0;
}

byte[] kept = Convert.FromBase64String(stored.StructuredContent!.Value.GetProperty("base64").GetString()!);
Console.WriteLine($"stored: {kept.Length} bytes, same as the original: {Hash(kept) == Hash(original)}");

// Step 3: download it again, the way a consumer would, and compare that too.
JsonElement download = await Curl(["-s", "-o", "back.bin", url]);
Console.WriteLine($"download exit: {download.GetProperty("exitCode")}");
Console.WriteLine($"download same as the original: {Hash(File.ReadAllBytes("back.bin")) == Hash(original)}");

// Step 4: the server's own record of the upload, the commands only: a binary transfer and the store.
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
IEnumerable<string> commands = recorded.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
    .Where(chunk => chunk.GetProperty("direction").GetString() == "received")
    .Select(chunk => chunk.GetProperty("data").GetString()!.TrimEnd('\r', '\n'))
    .Where(line => line.StartsWith("TYPE", StringComparison.Ordinal) || line.StartsWith("STOR", StringComparison.Ordinal));
Console.WriteLine("server received: " + string.Join(", ", commands));
return 0;

// One curl-mcp call: curl with these arguments. The answer is text, JSON, and a failed run is still an answer.
async Task<JsonElement> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
