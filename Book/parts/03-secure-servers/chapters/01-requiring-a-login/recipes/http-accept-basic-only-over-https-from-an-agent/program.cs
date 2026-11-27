// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a server that offers Basic alone, over HTTPS and plain HTTP, with surl-mcp, and logs in
// to both with curl-mcp, over the official MCP SDK. Run: dotnet run program.cs
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. server.pem and
// server.key, the certificate and key for 127.0.0.1, are in it.
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

// surl-mcp: one server, an https and an http listener, one account, Basic the only method.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--auth", "basic", "--user", "alice:s3cret" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string https = bound[0].GetString()! + "hello.txt";
string http = bound[1].GetString()! + "hello.txt";

await Fetch("https, right password", https, "--cacert", "server.pem", "-u", "alice:s3cret");
await Fetch("https, wrong password", https, "--cacert", "server.pem", "-u", "alice:guess");
await Fetch("https, no login", https, "--cacert", "server.pem");
await Fetch("https, Digest", https, "--cacert", "server.pem", "--digest", "-u", "alice:s3cret");
await Fetch("http, right password", http, "-u", "alice:s3cret");
await Fetch("http, wrong password", http, "-u", "alice:guess");
await Fetch("http, no login", http);
return 0;

// curl-mcp's curl tool runs curl's own arguments. -i puts the status line and headers before the body;
// --http1.1 keeps the header names as the server wrote them. The answer is JSON in text.
// A refusal's body is one line naming its status, so only a 200's body is printed.
async Task Fetch(string label, string url, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-i", "--http1.1" }.Concat(more).Append(url).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string[] head = output.Split("\r\n\r\n", 2)[0].Split("\r\n");
    string offered = string.Join("; ", head.Where(l => l.StartsWith("WWW-Authenticate: ")).Select(l => l[18..]));
    string status = head[0].Split(' ')[1];
    Console.WriteLine($"{label}: {status} [{offered}] {(status == "200" ? output.Split("\r\n\r\n", 2)[1] : "")}".TrimEnd());
}
