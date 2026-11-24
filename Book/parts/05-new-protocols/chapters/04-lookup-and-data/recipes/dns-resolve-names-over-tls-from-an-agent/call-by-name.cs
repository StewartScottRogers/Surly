// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent resolves a test name over DNS over TLS: surl-mcp serves the zone over TLS and the service it
// names, curl-mcp's curl tool looks the name up with a dns:// URL over TLS, and a second curl call reaches
// the service at the address the answer gave.
// Run: dotnet run call-by-name.cs [-- case]
//   (no argument)  look up api.example.test and call it
//   not-in-zone    look up old-api.example.test, a name the zone does not hold
//   untrusted      look up without trusting the server's certificate
//   own-resolver   call by name with curl's --dns-over-tls resolver instead
//   no-certificate start the DNS over TLS listener with no certificate
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "call";

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

// DNS over TLS alone on one free port, with the certificate setup made; the service on another.
string[] serverArgs = which == "no-certificate"
    ? ["--dns-transport", "tls"]
    : ["--cert", "server.pem", "--key", "server.key", "--dns-transport", "tls"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "dns://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = serverArgs,
    ["files"] = new Dictionary<string, string> { ["status.txt"] = "orders api: up\n" },
});
if (started.IsError == true)
{
    Console.WriteLine($"start refused: exit {started.StructuredContent!.Value.GetProperty("exit_code").GetInt32()}");
    return 1;
}

string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string dns = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
int httpPort = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!).Port;
await surl.CallToolAsync("add_dns_zone", new Dictionary<string, object?>
{
    ["server_id"] = id,
    ["apex"] = "example.test",
    ["zone_text"] = "$ORIGIN example.test.\n$TTL 300\n@ IN SOA ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300\n@ IN NS ns1.example.test.\napi IN A 127.0.0.1\n",
});
string name = which == "not-in-zone" ? "old-api.example.test" : "api.example.test";

if (which == "own-resolver")
{
    (int ownExit, string ownText) = await Curl("-s", "-S", "--max-time", "10", "--dns-over-tls", new Uri(dns).Authority, "--cacert", "server.pem", $"http://{name}:{httpPort}/status.txt");
    Console.Write($"curl exit {ownExit}\n{ownText}");
    return 1;
}

// The lookup: a dns:// URL over TLS, trusting the test server's certificate, the answer as JSON.
string[] lookup = which == "untrusted"
    ? ["-s", "-S", "--max-time", "10", "--dns-transport", "tls", "--dns-json", dns + name]
    : ["-s", "-S", "--max-time", "10", "--dns-transport", "tls", "--cacert", "server.pem", "--dns-json", dns + name];
(int exit, string text) = await Curl(lookup);
if (exit != 0)
{
    // The error text names the platform's TLS library; the exit code is the same everywhere.
    Console.WriteLine($"lookup: curl exit {exit}");
    return 1;
}

using JsonDocument answer = JsonDocument.Parse(text);
string status = answer.RootElement.GetProperty("status").GetString()!;
string? address = answer.RootElement.GetProperty("answer").EnumerateArray()
    .Where(r => r.GetProperty("type").GetString() == "A").Select(r => r.GetProperty("data").GetProperty("address").GetString()).FirstOrDefault();
Console.WriteLine($"{name}: {status}, {address ?? "no address"}");
if (address is null)
{
    return 1;
}

// The call: the URL keeps the name; --resolve connects that name and port to the address.
(int called, string body) = await Curl("-s", "-S", "--max-time", "10", "--resolve", $"{name}:{httpPort}:{address}", $"http://{name}:{httpPort}/status.txt");
Console.Write($"call: curl exit {called}\n{body}");
return called == 0 ? 0 : 1;

// One curl tool call: its exit code, and its standard output, or its standard error when it failed.
async Task<(int, string)> Curl(params string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    using JsonDocument reply = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int code = reply.RootElement.GetProperty("exitCode").GetInt32();
    return (code, reply.RootElement.GetProperty(code == 0 ? "standardOutput" : "standardError").GetProperty("content").GetString()!);
}
