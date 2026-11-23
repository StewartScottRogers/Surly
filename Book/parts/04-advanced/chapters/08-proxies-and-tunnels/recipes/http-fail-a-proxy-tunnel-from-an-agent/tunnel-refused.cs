// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an origin and a proxy that refuses every tunnel with surl-mcp, sends requests through
// the proxy with curl-mcp, and reads back from surl-mcp which side refused.
// Run: dotnet run tunnel-refused.cs -- <case>
//   refused   transfer of an https:// URL through the proxy, which needs a tunnel (the default)
//   forward   transfer of an http:// URL through the same proxy, forwarded without a tunnel
//   codes     the curl tool printing the proxy's and the origin's codes for the refused tunnel
//   busy      transfer of an http:// URL the origin itself answers with 503
//   forced    the curl tool with -p, tunnelling the http:// URL too
//   seen      the fault event the proxy kept, and the exchanges at the https origin
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "refused";

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

// One server: the origin over http and https with the certificate openssl wrote, a proxy listener,
// and two fault rules: CONNECT on the proxy is answered 503, and so is the origin's /busy.txt.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "https://127.0.0.1:0" },
    ["arguments"] = new[]
    {
        "--proxy", "http://127.0.0.1:0", "--cert", "server.pem", "--key", "server.key",
        "--fault", "error;scheme=http-proxy;command=CONNECT;reply=503",
        "--fault", "error;scheme=http;reply=503;path=/busy.txt",
    },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string plain = server.GetProperty("bound_urls")[0].GetString()!;
string secure = server.GetProperty("bound_urls")[1].GetString()!;
string proxy = server.GetProperty("bound_urls")[2].GetString()!;

if (which is "codes" or "forced")
{
    // The curl tool takes curl's own words: -w for the two codes, -p to force a tunnel.
    string[] words = which == "codes"
        ? ["-s", "--max-time", "20", "--cacert", "server.pem", "-o", "out.txt", "-w", "%{http_connect} %{http_code}", "-x", proxy, secure + "hello.txt"]
        : ["-sS", "--max-time", "20", "-p", "-x", proxy, plain + "hello.txt"];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string? stdout = run.GetProperty("standardOutput").GetProperty("content").GetString();
    string? stderr = run.GetProperty("standardError").GetProperty("content").GetString();
    Console.WriteLine($"curl tool: exitCode {run.GetProperty("exitCode")}, {(which == "codes" ? stdout : stderr!.TrimEnd())}");
    return 0;
}

// curl-mcp transfer: proxy is curl -x, caFile is --cacert for the origin.
string url = which switch { "forward" => plain + "hello.txt", "busy" => plain + "busy.txt", _ => secure + "hello.txt" };
CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?>
{
    ["url"] = url, ["proxy"] = proxy, ["caFile"] = "server.pem", ["timeoutSeconds"] = 20,
});
JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
JsonElement failure = answer.GetProperty("failure");
Console.WriteLine(failure.ValueKind == JsonValueKind.Null
    ? $"transfer: statusCode {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}, tool error: {(fetched.IsError == true ? "yes" : "no")}"
    : $"transfer: exitCode {failure.GetProperty("exitCode")}, {failure.GetProperty("text").GetString()!.Split('\n')[0]}, tool error: {(fetched.IsError == true ? "yes" : "no")}");

if (which == "seen")
{
    // surl-mcp get_events: the fault_applied event the proxy listener kept for the refused CONNECT.
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "fault_applied" }, ["listen_url"] = proxy,
    });
    foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
        Console.WriteLine($"fault_applied: rule {e.GetProperty("rule_id")}, kind {e.GetProperty("fault_kind")}, stage {e.GetProperty("stage")}");
    // surl-mcp get_exchanges for the https origin: nothing ever reached it.
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["listen_url"] = secure,
    });
    Console.WriteLine($"exchanges at the https origin: {exchanges.StructuredContent!.Value.GetProperty("exchanges").GetArrayLength()}");
}
return 0;
