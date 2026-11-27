// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts an HTTPS server that requires a client certificate (surl-mcp) and fetches from it
// with curl-mcp's curl tool, presenting a certificate or not.
// Run: dotnet run program.cs -- <authority> <client>
//   authority  ca.pem (the test authority), none (ask for no certificate), or another file
//   client     p12 (client.p12), pem (client.pem and client.key), stranger (stranger.p12), or none
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string authority = args[0], client = args[1];

// Both hosts are confined to this folder, which holds the certificates openssl made.
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

// surl-mcp: the server's certificate and key, and --cacert, the authority a client certificate must come from.
var arguments = new List<string> { "--cert", "server.pem", "--key", "server.key" };
if (authority != "none")
{
    arguments.AddRange(["--cacert", authority]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    Console.WriteLine($"exit_code: {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")})");
    Console.WriteLine(server.GetProperty("lines")[0].GetString());
    return server.GetProperty("exit_code").GetInt32();
}

string serverId = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp's curl tool, since transfer has no field for a client certificate.
var curlArgs = new List<string> { "-sS", "--max-time", "10", "--cacert", "server.pem" };
curlArgs.AddRange(client switch
{
    "p12" => ["--cert", "client.p12:s3cret", "--cert-type", "P12"],
    "stranger" => ["--cert", "stranger.p12:s3cret", "--cert-type", "P12"],
    "pem" => ["--cert", "client.pem", "--key", "client.key"],
    _ => [],
});
curlArgs.Add(url + "hello.txt");
CallToolResult fetched = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
int exitCode = answer.GetProperty("exitCode").GetInt32();
if (exitCode == 0)
{
    Console.Write(answer.GetProperty("standardOutput").GetProperty("content").GetString());
    return 0;
}

Console.WriteLine($"curl failed: {(fetched.IsError == true ? "tool error" : "no error")}, exitCode {exitCode}");
Console.Write(answer.GetProperty("standardError").GetProperty("content").GetString());

// surl-mcp: what the server recorded of that connection. A refused handshake ends with no request read.
CallToolResult events = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = serverId, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 5000,
});
JsonElement ended = events.StructuredContent!.Value.GetProperty("event");
Console.WriteLine($"server: exchange {ended.GetProperty("exchange_id")} ended, {ended.GetProperty("received_byte_count")} bytes received");
return 1;
