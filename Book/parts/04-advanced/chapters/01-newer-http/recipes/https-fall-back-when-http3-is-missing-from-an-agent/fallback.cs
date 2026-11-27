// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent asks for HTTP/3 through curl-mcp's curl tool from a surl-mcp server with no HTTP/3,
// and reports whether the request fell back to TCP or failed.
// Run: dotnet run fallback.cs [-- only | swallowed | swallowed-only | h3]
//   (no argument)  curl --http3 against nothing on UDP
//   only           curl --http3-only --connect-timeout 2 against nothing on UDP
//   swallowed      curl --http3 against a TFTP listener on the same port number
//   swallowed-only curl --http3-only --connect-timeout 2 against it
//   h3             curl --http3 against a server started with --http3
#:package ModelContextProtocol.Core@2.4.0

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

var files = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" };

// surl-mcp: HTTPS with the certificate and key from setup; --http3 only for h3.
var arguments = new List<string> { "--cert", "server.pem", "--key", "server.key" };
if (mode == "h3")
{
    arguments.Add("--http3");
}

JsonElement server = await StartHttps();
string url = server.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

// With swallowed, a TFTP server takes the same port number on UDP: QUIC packets then reach a
// listener that does not speak QUIC. A UDP port can be taken when its TCP twin is free, so on
// a refusal both servers start again on another port.
string? tftpId = null;
for (int attempt = 1; mode.StartsWith("swallowed") && tftpId is null && attempt <= 5; attempt++)
{
    int port = new Uri(url).Port;
    CallToolResult tftp = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { $"tftp://127.0.0.1:{port}" },
        ["files"] = files,
    });
    if (tftp.IsError != true)
    {
        tftpId = tftp.StructuredContent!.Value.GetProperty("server_id").GetString();
        break;
    }

    await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = server.GetProperty("server_id").GetString() });
    server = await StartHttps();
    url = server.GetProperty("bound_urls")[0].GetString()! + "hello.txt";
}

// curl-mcp's curl tool: --http3 may fall back to TCP, --http3-only may not, and is bounded
// by --connect-timeout so a QUIC attempt nobody answers cannot hold the call.
var curlArgs = new List<string> { mode.EndsWith("only") ? "--http3-only" : "--http3", "--cacert", "server.pem" };
if (mode.EndsWith("only"))
{
    curlArgs.AddRange(["--connect-timeout", "2"]);
}

if (mode == "h3")
{
    curlArgs.AddRange(["--happy-eyeballs-timeout-ms", "4000"]);
}

curlArgs.AddRange(["-s", "-o", "hello.txt", "-w", "%{http_version}", url]);
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
Console.WriteLine($"http_version: {answer.GetProperty("standardOutput").GetProperty("content").GetString()}");
JsonElement failure = answer.GetProperty("failure");
Console.WriteLine($"failure: {(failure.ValueKind == JsonValueKind.Null ? "null" : failure.GetProperty("text").GetString())}");
Console.WriteLine($"tool error: {(ran.IsError == true ? "yes" : "no")}");

// surl-mcp's list_servers: the TFTP server counts every QUIC packet it took as an exchange.
if (tftpId is not null)
{
    JsonElement listed = (await surl.CallToolAsync("list_servers", new Dictionary<string, object?>())).StructuredContent!.Value;
    int count = listed.GetProperty("servers").EnumerateArray()
        .Single(s => s.GetProperty("server_id").GetString() == tftpId).GetProperty("exchange_count").GetInt32();
    Console.WriteLine($"QUIC packets the TFTP listener took as exchanges: {(count > 0 ? "some" : "none")}");
}

return ran.IsError == true ? 1 : 0;

async Task<JsonElement> StartHttps() =>
    (await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "https://127.0.0.1:0" },
        ["arguments"] = arguments,
        ["files"] = files,
    })).StructuredContent!.Value;
