// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds binary files, an image and an archive, into a surl server with base64, through
// surl-mcp, and fetches them with curl through curl-mcp, checking them byte for byte by SHA-256.
// Run: dotnet run program.cs -- <case>
//   solution          start_server with files_base64; transfer each file and compare it with the seed
//   put-file          put_file with base64 on a running server
//   read-file         read_file on a binary file answers base64, on a text file text
//   to-disk           the curl tool writes the archive to disk; the file is the seed
//   content-types     the Content-Type of five names
//   split-seed        six 512 KiB files, one put_file call each, fetched to disk
//   transfer-limit    a 2 MiB file: transfer answers part of it, curl -o all of it
//   bad-base64        put_file and start_server given text that is not base64
//   both-given        put_file with text and base64, start_server with one path twice
#:package ModelContextProtocol.Core@2.4.0

using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// The files: bytes that are not text. A PNG signature and an archive's local-header signature, then
// pseudo-random bytes from a fixed seed, so every run holds the same ones.
byte[] Blob(int seed, int length, params byte[] start)
{
    var bytes = new byte[length];
    new Random(seed).NextBytes(bytes);
    start.CopyTo(bytes, 0);
    return bytes;
}
byte[] logo = Blob(1, 3000, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
byte[] pack = Blob(2, 5000, 0x50, 0x4B, 0x03, 0x04);

// surl-mcp: a server on a free port. files_base64 maps a path to base64; files maps it to text.
var seeded = new Dictionary<string, string> { ["logo.png"] = Convert.ToBase64String(logo), ["pack.zip"] = Convert.ToBase64String(pack) };
if (which == "content-types")
{
    foreach (string name in new[] { "a.jpg", "b.pdf", "c.bin" })
    {
        seeded[name] = Convert.ToBase64String(logo);
    }
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files_base64"] = seeded,
    ["files"] = new Dictionary<string, string> { ["notes.txt"] = "plain text\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "solution":
        await Check("logo.png", logo);
        await Check("pack.zip", pack);
        break;
    case "put-file":
        byte[] icon = Blob(3, 1200, 0x89, 0x50, 0x4E, 0x47);
        Console.WriteLine("put_file: " + Text(await surl.CallToolAsync("put_file", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["path"] = "/images/icon.png", ["base64"] = Convert.ToBase64String(icon),
        })));
        await Check("images/icon.png", icon);
        break;
    case "read-file":
        foreach (string path in new[] { "/logo.png", "/notes.txt" })
        {
            CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = server, ["path"] = path });
            JsonElement file = read.StructuredContent!.Value;
            bool isBinary = file.TryGetProperty("base64", out JsonElement b64);
            Console.WriteLine($"{path}: {file.GetProperty("content_type").GetString()}, {file.GetProperty("byte_count").GetInt32()} bytes, "
                + (isBinary ? $"base64, identical: {Same(Convert.FromBase64String(b64.GetString()!), logo)}" : $"text {file.GetProperty("text").GetString()!.Trim()}"));
        }

        break;
    case "to-disk":
        await ToDisk("pack.zip", "got/pack.zip");
        Console.WriteLine($"got/pack.zip: {File.ReadAllBytes("got/pack.zip").Length} bytes, identical: {Same(File.ReadAllBytes("got/pack.zip"), pack)}");
        break;
    case "content-types":
        foreach (string name in new[] { "logo.png", "pack.zip", "a.jpg", "b.pdf", "c.bin" })
        {
            using JsonDocument answer = await Transfer(name);
            Console.WriteLine($"{name}: {ContentType(answer)}");
        }

        break;
    case "split-seed":
        // One file a call: each message stays far below surl-mcp's 4 MiB limit.
        var blobs = Enumerable.Range(1, 6).Select(i => Blob(10 + i, 512 * 1024)).ToList();
        for (int i = 0; i < blobs.Count; i++)
        {
            await surl.CallToolAsync("put_file", new Dictionary<string, object?>
            {
                ["server_id"] = server, ["path"] = $"/part{i + 1}.bin", ["base64"] = Convert.ToBase64String(blobs[i]),
            });
            await ToDisk($"part{i + 1}.bin", $"got/part{i + 1}.bin");
        }

        Console.WriteLine($"identical: {Enumerable.Range(0, 6).Count(i => Same(File.ReadAllBytes($"got/part{i + 1}.bin"), blobs[i]))} of 6");
        break;
    case "transfer-limit":
        byte[] big = Blob(20, 2 * 1024 * 1024);
        await surl.CallToolAsync("put_file", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["path"] = "/big.bin", ["base64"] = Convert.ToBase64String(big),
        });
        using (JsonDocument answer = await Transfer("big.bin"))
        {
            JsonElement body = answer.RootElement.GetProperty("body");
            Console.WriteLine($"transfer: {body.GetProperty("returnedBytes").GetInt32()} of {body.GetProperty("totalBytes").GetInt32()} bytes, truncated: {body.GetProperty("isTruncated").GetBoolean()}");
        }

        await ToDisk("big.bin", "got/big.bin");
        Console.WriteLine($"curl -o: {File.ReadAllBytes("got/big.bin").Length} bytes, identical: {Same(File.ReadAllBytes("got/big.bin"), big)}");
        break;
    case "bad-base64":
        Console.WriteLine("put_file: " + Text(await surl.CallToolAsync("put_file", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["path"] = "/bad.bin", ["base64"] = "not base64!",
        })));
        Console.WriteLine("start_server: " + Text(await surl.CallToolAsync("start_server", new Dictionary<string, object?>
        {
            ["listen_urls"] = new[] { "http://127.0.0.1:0" }, ["files_base64"] = new Dictionary<string, string> { ["bad.bin"] = "not base64!" },
        })));
        break;
    case "both-given":
        Console.WriteLine("put_file: " + Text(await surl.CallToolAsync("put_file", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["path"] = "/both.bin", ["base64"] = "AAEC", ["text"] = "x",
        })));
        Console.WriteLine("start_server: " + Text(await surl.CallToolAsync("start_server", new Dictionary<string, object?>
        {
            ["listen_urls"] = new[] { "http://127.0.0.1:0" },
            ["files_base64"] = new Dictionary<string, string> { ["same.bin"] = "AAEC" },
            ["files"] = new Dictionary<string, string> { ["same.bin"] = "x" },
        })));
        break;
}

return 0;

// curl-mcp's transfer answers JSON text; a binary body comes back as base64 with isBase64 true.
async Task<JsonDocument> Transfer(string path)
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
    return JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text);
}

static string ContentType(JsonDocument answer) => answer.RootElement.GetProperty("headers").EnumerateArray()
    .First(h => h.GetProperty("name").GetString() == "Content-Type").GetProperty("value").GetString()!;

async Task Check(string path, byte[] expected)
{
    using JsonDocument answer = await Transfer(path);
    JsonElement body = answer.RootElement.GetProperty("body");
    byte[] got = body.GetProperty("isBase64").GetBoolean() ? Convert.FromBase64String(body.GetProperty("content").GetString()!) : [];
    Console.WriteLine($"{path}: {answer.RootElement.GetProperty("statusCode").GetInt32()} {ContentType(answer)} {got.Length} bytes, identical: {Same(got, expected)}");
}

// The curl tool takes a command line's arguments; -o with --create-dirs writes under CURL_MCP_ROOT.
async Task ToDisk(string path, string file)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-f", "--create-dirs", "-o", file, url + path },
    });
    if (ran.IsError == true)
    {
        Console.WriteLine("curl failed: " + Text(ran));
    }
}

static bool Same(byte[] a, byte[] b) => SHA256.HashData(a).SequenceEqual(SHA256.HashData(b));

// A tool error is text that says why; a success is structured content.
static string Text(CallToolResult result) => result.IsError == true
    ? "error " + string.Join(" ", result.Content.OfType<TextContentBlock>().Select(t => t.Text))
    : result.StructuredContent?.ToString() ?? "";
