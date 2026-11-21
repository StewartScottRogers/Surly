// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes two requests through curl-mcp's curl tool with one alt-svc file, against a
// surl-mcp server with --http3, and asks the server which transport each came over.
// Run: dotnet run move-to-http3.cs [-- none | http1.1]
//   (no argument) the second request adds --http3
//   none          the second request has no version option
//   http1.1       the second request adds --http1.1
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "";

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

// surl-mcp: HTTPS with the certificate and key from setup, and HTTP/3 beside it.
JsonElement server = (await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--http3" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
})).StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

// First request: nothing known yet, so TCP; curl writes the answer's Alt-Svc to alt-svc.txt.
File.Delete("alt-svc.txt");
await Fetch("first", []);
string saved = File.ReadLines("alt-svc.txt").Single(line => !line.StartsWith('#'));
string[] f = saved.Split(' ');
Console.WriteLine($"saved: {f[0]} {f[1]} to {f[3]} {f[4]} on {(f[2] == f[5] ? "the same port" : "another port")}");

// Second request: curl reads the entry before it connects. --happy-eyeballs-timeout-ms holds TCP
// back 4 s while QUIC finishes its handshake.
await Fetch("second", mode switch { "none" => [], "http1.1" => ["--http1.1"], _ => ["--http3"] });
return 0;

async Task Fetch(string which, string[] version)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = version.Concat(new[]
        {
            "--alt-svc", "alt-svc.txt", "--happy-eyeballs-timeout-ms", "4000", "--cacert", "server.pem",
            "-s", "-o", "hello.txt", "-w", "HTTP/%{http_version}, Alt-Svc [%header{alt-svc}]", url,
        }).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{which}: exit {answer.GetProperty("exitCode")}, {answer.GetProperty("standardOutput").GetProperty("content").GetString()}");

    // surl-mcp's get_exchanges: a QUIC exchange records bytes per stream, with a stream_id; TCP has none.
    JsonElement seen = (await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId })).StructuredContent!.Value;
    JsonElement newest = seen.GetProperty("exchanges").EnumerateArray().Last();
    bool quic = newest.GetProperty("chunks").EnumerateArray().Any(c => c.TryGetProperty("stream_id", out _));
    Console.WriteLine($"{which}: the server saw {(quic ? "QUIC" : "TCP")}");
}
