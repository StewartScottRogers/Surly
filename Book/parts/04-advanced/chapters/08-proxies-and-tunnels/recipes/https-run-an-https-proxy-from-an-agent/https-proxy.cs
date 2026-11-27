// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an https proxy with its own certificate and an https origin with another through
// surl-mcp, and fetches through the proxy with curl-mcp, trusting each peer separately.
// Run: dotnet run https-proxy.cs -- <case>
//   fetch             transfer of an http:// origin through the https proxy, trusting the proxy (the default)
//   untrusted         the same, trusting nothing
//   origin-trust      the same, with the proxy's certificate as caFile, the origin's authority
//   seen              what the proxy received, from get_exchanges
//   both              transfer of an https:// origin, proxyCaFile and caFile each with its own certificate
//   swapped           the same, with the two certificates swapped
//   origin-unchecked  transfer with insecure true, the proxy trusted by nobody
//   proxy-unchecked   the curl tool with --proxy-insecure, the origin trusted
//   no-certificate    start_server with an https proxy and no certificate
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fetch";

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
var hello = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" };

if (which == "no-certificate")
{
    CallToolResult refused = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "http://127.0.0.1:0" }, ["arguments"] = new[] { "--proxy", "https://127.0.0.1:0" },
    });
    JsonElement why = refused.StructuredContent!.Value;
    Console.WriteLine($"tool error: {(refused.IsError == true ? "yes" : "no")}, exit_code {why.GetProperty("exit_code")} {why.GetProperty("exit_code_name")}: {why.GetProperty("lines")[0]}");
    return 0;
}

// The proxy's server: an https proxy listener with the proxy's certificate, and a plain http origin.
CallToolResult proxyStarted = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--proxy", "https://127.0.0.1:0", "--cert", "proxy.pem", "--key", "proxy.key" },
    ["files"] = hello,
});
JsonElement proxyServer = proxyStarted.StructuredContent!.Value;
string plainOrigin = proxyServer.GetProperty("bound_urls")[0].GetString()!;
string proxy = proxyServer.GetProperty("bound_urls")[1].GetString()!;

// The https origin, with a certificate of its own.
CallToolResult originStarted = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "origin.pem", "--key", "origin.key" },
    ["files"] = hello,
});
string secureOrigin = originStarted.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

if (which == "proxy-unchecked")
{
    // transfer has no field for --proxy-insecure: the curl tool takes curl's own words.
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "--max-time", "20", "--proxy-insecure", "--cacert", "origin.pem", "-x", proxy, secureOrigin + "hello.txt" },
    });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"curl tool: exitCode {run.GetProperty("exitCode")}, {run.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd()}");
    return 0;
}

// curl-mcp transfer: proxyCaFile checks the proxy (--proxy-cacert), caFile the origin (--cacert),
// insecure skips the origin's check only (-k).
var fields = new Dictionary<string, object?> { ["proxy"] = proxy, ["timeoutSeconds"] = 20, ["url"] = plainOrigin + "hello.txt" };
switch (which)
{
    case "fetch" or "seen": fields["proxyCaFile"] = "proxy.pem"; break;
    case "origin-trust": fields["caFile"] = "proxy.pem"; break;
    case "both": fields["proxyCaFile"] = "proxy.pem"; fields["caFile"] = "origin.pem"; fields["url"] = secureOrigin + "hello.txt"; break;
    case "swapped": fields["proxyCaFile"] = "origin.pem"; fields["caFile"] = "proxy.pem"; fields["url"] = secureOrigin + "hello.txt"; break;
    case "origin-unchecked": fields["insecure"] = true; fields["url"] = secureOrigin + "hello.txt"; break;
}
CallToolResult fetched = await curl.CallToolAsync("transfer", fields);
JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
JsonElement failure = answer.GetProperty("failure");
Console.WriteLine(failure.ValueKind == JsonValueKind.Null
    ? $"transfer: statusCode {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}"
    : $"transfer: exitCode {failure.GetProperty("exitCode")}, tool error: {(fetched.IsError == true ? "yes" : "no")}");

if (which == "seen")
{
    // surl-mcp get_exchanges on the proxy's listen URL: the request it decrypted, as text.
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = proxyServer.GetProperty("server_id").GetString(), ["listen_url"] = proxy,
    });
    JsonElement head = exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(x => x.GetProperty("chunks").EnumerateArray())
        .First(c => c.GetProperty("direction").GetString() == "received" && c.GetProperty("encoding").GetString() == "text");
    Console.WriteLine($"the proxy received: {head.GetProperty("data").GetString()!.Split("\r\n")[0]}");
}
return 0;
