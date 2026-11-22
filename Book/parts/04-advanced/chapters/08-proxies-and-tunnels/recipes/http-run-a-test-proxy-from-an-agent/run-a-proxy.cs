// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an origin and an HTTP proxy with surl-mcp, fetches through the proxy with curl-mcp,
// and reads back what each listener received.
// Run: dotnet run run-a-proxy.cs -- <case>
//   fetch           fetch hello.txt from the origin through the proxy (the default)
//   outside         ask the proxy for an origin outside loopback, 192.0.2.1
//   tunnel-outside  the same through a CONNECT tunnel, with the curl tool and -p
//   allowed         the same from a proxy started with --proxy-allow-origin 192.0.2.1
//   every-origin    start a proxy with --proxy-allow-origin * and show its log lines
#:package ModelContextProtocol.Core@*

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

// One server: the origin on the listen URL, and a proxy from --proxy in arguments.
List<string> arguments = ["--proxy", "http://127.0.0.1:0"];
if (which == "allowed") arguments.AddRange(["--proxy-allow-origin", "192.0.2.1", "--head-timeout", "2"]);
if (which == "every-origin") arguments.AddRange(["--proxy-allow-origin", "*"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string origin = server.GetProperty("bound_urls")[0].GetString()!;
string proxy = server.GetProperty("bound_urls")[1].GetString()!;

if (which == "every-origin")
{
    foreach (JsonElement line in server.GetProperty("log_lines").EnumerateArray()) Console.WriteLine(line.GetString());
    return 0;
}

string target = which == "fetch" ? origin + "hello.txt" : "http://192.0.2.1/hello.txt";
if (which == "tunnel-outside")
{
    // transfer has no tunnel field: the curl tool takes curl's own -p.
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-sS", "-p", "-x", proxy, target } });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode: {run.GetProperty("exitCode")}");
    Console.WriteLine(run.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd());
    Console.WriteLine($"tool error: {(ran.IsError == true ? "yes" : "no")}");
    return 0;
}

// curl-mcp: one request through the proxy, as curl -x sends it. It answers in text, as JSON.
CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = target, ["proxy"] = proxy });
JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string via = answer.GetProperty("headers").EnumerateArray()
    .Where(h => h.GetProperty("name").GetString() == "Via").Select(h => h.GetProperty("value").GetString()).FirstOrDefault() ?? "none";
Console.WriteLine($"statusCode: {answer.GetProperty("statusCode")}, Via: {via}, failure: {answer.GetProperty("failure").GetRawText()}");

// surl-mcp: the request line each listener received, and its Via header if any. The record can trail
// curl's answer by a moment: ask again until every exchange holds a whole request head, for at most 10 s.
JsonElement recorded = default;
for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); ; await Task.Delay(100))
{
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server.GetProperty("server_id").GetString() });
    recorded = exchanges.StructuredContent!.Value.GetProperty("exchanges").Clone();
    bool whole = recorded.EnumerateArray().All(x => string.Concat(x.GetProperty("chunks").EnumerateArray()
        .Where(c => c.GetProperty("direction").GetString() == "received").Select(c => c.GetProperty("data").GetString())).Contains("\r\n\r\n"));
    if (whole || DateTime.UtcNow >= deadline) break;
}

foreach (JsonElement exchange in recorded.EnumerateArray())
{
    string head = exchange.GetProperty("chunks").EnumerateArray()
        .First(c => c.GetProperty("direction").GetString() == "received").GetProperty("data").GetString()!;
    string[] lines = head.Split("\r\n");
    string listener = exchange.GetProperty("listen_url").GetString() == proxy ? "proxy" : "origin";
    Console.WriteLine($"{listener} received: {string.Join(" | ", lines.Where(l => l.StartsWith("Via:")).Prepend(lines[0]))}");
}

Console.WriteLine($"tool error: {(fetched.IsError == true ? "yes" : "no")}");
return fetched.IsError == true ? 1 : 0;
