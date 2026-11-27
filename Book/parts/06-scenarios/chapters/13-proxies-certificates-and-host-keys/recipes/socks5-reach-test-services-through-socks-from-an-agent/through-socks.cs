// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent on a network that allows only a SOCKS5 proxy: surl-mcp runs HTTP and FTP services and
// the proxy, curl-mcp's transfer tool calls both services through the proxy, and the agent reads
// the proxy's record with get_exchanges.
// Run: dotnet run through-socks.cs [-- <case>]
//   (no argument)  HTTP and FTP through the proxy, then the proxy's record
//   locked         FTP through the proxy to a service that takes no anonymous login
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "calls";

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

// The test network: HTTP and FTP on localhost, and the SOCKS5 proxy, in one server.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://localhost:0", "ftp://localhost:0", "socks5h://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string[] bound = started.StructuredContent!.Value.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();
(int httpPort, int ftpPort, string socks) = (new Uri(bound[0]).Port, new Uri(bound[1]).Port, bound[2]);

// An FTP service as surl starts by default: no anonymous login.
CallToolResult lockedStarted = await surl.CallToolAsync("start_server", new Dictionary<string, object?> { ["listen_urls"] = new[] { "ftp://localhost:0" } });
int lockedPort = new Uri(lockedStarted.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;

// Every call goes through the proxy, which resolves localhost (socks5h).
string[] targets = mode == "locked"
    ? [$"ftp://localhost:{lockedPort}/hello.txt"]
    : [$"http://localhost:{httpPort}/hello.txt", $"ftp://localhost:{ftpPort}/hello.txt"];
foreach (string target in targets)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?>
    {
        ["url"] = target, ["proxy"] = $"socks5h://127.0.0.1:{new Uri(socks).Port}", ["timeoutSeconds"] = 20,
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string scheme = target[..target.IndexOf(':')];
    Console.WriteLine(result.IsError == true
        ? $"{scheme}: {answer.RootElement.GetProperty("failure").GetProperty("text").GetString()!.Split('\n').Last()}"
        : $"{scheme}: {answer.RootElement.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}");
}

// The FTP passive port: the one in the FTP service's 229 reply, from its own record.
CallToolResult ftp = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["listen_url"] = bound[1] });
int passivePort = ftp.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
    .SelectMany(e => e.GetProperty("chunks").EnumerateArray())
    .Select(c => c.GetProperty("data").GetString()!)
    .Where(d => d.StartsWith("229 ")).Select(d => int.Parse(d.Split("|||")[1].Split('|')[0])).FirstOrDefault();

// The proxy's record: each exchange on its listen URL. The client's second message is its request,
// 05 01 00 03, the name's length, the name, and the port in two bytes (RFC 1928).
CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["listen_url"] = socks });
foreach (JsonElement exchange in got.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    byte[] request = exchange.GetProperty("chunks").EnumerateArray()
        .Where(c => c.GetProperty("direction").GetString() == "received")
        .Select(c => Convert.FromBase64String(c.GetProperty("data").GetString()!)).ElementAt(1);
    string host = Encoding.ASCII.GetString(request, 5, request[4]);
    int port = request[5 + request[4]] << 8 | request[6 + request[4]];
    string what = port == httpPort ? "the HTTP service" : port == ftpPort ? "the FTP service" : port == lockedPort ? "the locked FTP service"
        : port == passivePort ? "the FTP passive port from the 229 reply" : "another port";
    Console.WriteLine($"proxy exchange: {host}, {what}");
}

return 0;
