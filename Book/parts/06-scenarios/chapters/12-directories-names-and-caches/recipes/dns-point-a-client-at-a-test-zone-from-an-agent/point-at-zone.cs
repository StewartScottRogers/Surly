// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent points a client at a test zone: surl-mcp serves the zone over DNS over HTTPS and the service
// the zone names, add_dns_zone seeds the zone, and curl-mcp's curl tool calls the service by name with
// its lookups sent to the test server.
// Run: dotnet run point-at-zone.cs [-- case]
//   (no argument)  seed the zone, then call api.example.test
//   before-seed    call api.example.test before the zone is seeded
//   not-in-zone    seed the zone, then call old-api.example.test
//   untrusted      seed the zone, then call without trusting the DNS server's certificate
//   read-back      seed the zone, then read it back
#:package ModelContextProtocol.Core@2.4.0

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

// DNS over HTTPS on one free port, with the certificate setup made; the service on another.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "dns://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--dns-transport", "https" },
    ["files"] = new Dictionary<string, string> { ["status.txt"] = "orders api: up\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int dnsPort = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;
int httpPort = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!).Port;

// The test zone: the orders service, api, at the loopback address.
if (which != "before-seed")
{
    await surl.CallToolAsync("add_dns_zone", new Dictionary<string, object?>
    {
        ["server_id"] = id,
        ["apex"] = "example.test",
        ["zone_text"] = "$ORIGIN example.test.\n$TTL 300\n@ IN SOA ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300\n@ IN NS ns1.example.test.\napi IN A 127.0.0.1\n",
    });
}

if (which == "read-back")
{
    CallToolResult zone = await surl.CallToolAsync("read_zone", new Dictionary<string, object?> { ["server_id"] = id, ["apex"] = "example.test" });
    Console.Write(zone.StructuredContent!.Value.GetProperty("zone").GetString());
    return 0;
}

// The client's call by name; --doh-url sends its lookups to the test server, --cacert trusts its certificate.
string name = which == "not-in-zone" ? "old-api.example.test" : "api.example.test";
var curlArgs = new List<string> { "-s", "-S", "--max-time", "10", "--doh-url", $"https://127.0.0.1:{dnsPort}/dns-query" };
if (which != "untrusted")
{
    curlArgs.AddRange(["--cacert", "server.pem"]);
}

curlArgs.Add($"http://{name}:{httpPort}/status.txt");
CallToolResult called = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
using JsonDocument answer = JsonDocument.Parse(called.Content.OfType<TextContentBlock>().Single().Text);
int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
Console.WriteLine($"curl exit {exit}, tool error {called.IsError == true}");
Console.Write(answer.RootElement.GetProperty(exit == 0 ? "standardOutput" : "standardError").GetProperty("content").GetString());
return exit == 0 ? 0 : 1;
