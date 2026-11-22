// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent downloads a file with curl-mcp and keeps it only if its SHA-256 is the published one;
// surl-mcp serves the file and, in one scenario, changes it.
// Run: dotnet run fetch-verified.cs -- <case>
//   match         download tool-1.0.bin and check it against the published digest (the default)
//   wrong-digest  check the same download against another digest
//   changed       keep a good copy, change the served file with put_file, and download it again
//   missing       download a file the server does not have
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "match";
const string Published = "0e2fb8e221331b806feda5547ebd3fa59614053f86f9f0d92b8e840976e0e80c";

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

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["tool-1.0.bin"] = "installer for tool 1.0\n" },
});
JsonElement server = started.StructuredContent!.Value;
string url = server.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "wrong-digest":
        return await FetchVerified("tool-1.0.bin", "3a781f0b1f6c0a682e41bd887f6307082508282ab9c96d8a6bf1dc62f0a237c2", "other.bin");
    case "changed":
        await FetchVerified("tool-1.0.bin", Published, "tool-1.0.bin");
        await surl.CallToolAsync("put_file", new Dictionary<string, object?>
        {
            ["server_id"] = server.GetProperty("server_id").GetString(),
            ["path"] = "/tool-1.0.bin",
            ["text"] = "installer for tool 1.0, with something added\n",
        });
        int refused = await FetchVerified("tool-1.0.bin", Published, "tool-1.0.bin");
        Console.WriteLine($"on disk: tool-1.0.bin {File.ReadAllText("tool-1.0.bin").TrimEnd()}");
        return refused;
    case "missing":
        int missing = await FetchVerified("tool-2.0.bin", Published, "tool-2.0.bin");
        Console.WriteLine($"on disk: tool-2.0.bin {(File.Exists("tool-2.0.bin") ? "written" : "missing")}");
        return missing;
    default:
        return await FetchVerified("tool-1.0.bin", Published, "tool-1.0.bin");
}

// Downloads path with transfer, hashes the body's bytes, and writes file only if the digest matches.
async Task<int> FetchVerified(string path, string want, string file)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    int status = answer.GetProperty("statusCode").GetInt32();
    JsonElement body = answer.GetProperty("body");
    if (status >= 400)
    {
        Console.WriteLine($"refused {file}: statusCode {status}");
        return 22;
    }

    // A body cut short would hash wrong: check it all came back before trusting the digest.
    if (body.GetProperty("isTruncated").GetBoolean())
    {
        Console.WriteLine($"refused {file}: body truncated at {body.GetProperty("returnedBytes")} of {body.GetProperty("totalBytes")} bytes");
        return 1;
    }

    string content = body.GetProperty("content").GetString()!;
    byte[] bytes = body.GetProperty("isBase64").GetBoolean() ? Convert.FromBase64String(content) : Encoding.UTF8.GetBytes(content);
    string got = Convert.ToHexStringLower(SHA256.HashData(bytes));
    if (got != want)
    {
        Console.WriteLine($"refused {file}: SHA-256 is {got}, not {want}");
        return 1;
    }

    File.WriteAllBytes(file, bytes);
    Console.WriteLine($"kept {file}: SHA-256 {got}, {bytes.Length} bytes");
    return 0;
}
