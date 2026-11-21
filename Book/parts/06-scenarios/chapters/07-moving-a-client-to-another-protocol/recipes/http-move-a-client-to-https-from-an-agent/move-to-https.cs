// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent moves a client from HTTP to HTTPS: surl-mcp serves both during the move and HTTPS
// alone after it; curl-mcp is the client; get_exchanges is the record of what came in.
// Run: dotnet run move-to-https.cs -- <step>
//   moved           during the move: the moved client fetches over https (the default)
//   unmoved         during the move: a client not yet moved fetches over http
//   guard           during the move: a plain URL left in the moved client
//   untrusted       during the move: an https fetch without the test certificate as authority
//   record          during the move: all three calls, then the scheme of every exchange recorded
//   after           after the move: the server again on its https address alone; the moved client
//   after-plain     after the move: a client still on the plain URL
//   after-record    after the move: both clients, then the scheme of every exchange recorded
//   no-certificate  an https listen URL with no certificate
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string step = args.Length > 0 ? args[0] : "moved";

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

if (step == "no-certificate")
{
    CallToolResult refused = await surl.CallToolAsync("start_server", new Dictionary<string, object?> { ["listen_urls"] = new[] { "https://127.0.0.1:0" } });
    JsonElement why = refused.StructuredContent!.Value;
    Console.WriteLine($"exit_code: {why.GetProperty("exit_code")} ({why.GetProperty("exit_code_name")})");
    Console.WriteLine($"tool error: {(refused.IsError == true ? "yes" : "no")}");
    return 0;
}

// During the move: the same file over plain HTTP and HTTPS, with the certificate setup: made.
CallToolResult started = await Start("http://127.0.0.1:0", "https://127.0.0.1:0");
JsonElement server = started.StructuredContent!.Value;
string plain = server.GetProperty("bound_urls")[0].GetString()! + "hello.txt";
string secure = server.GetProperty("bound_urls")[1].GetString()! + "hello.txt";

if (step.StartsWith("after"))
{
    // After the move: stop the server and start it again on its HTTPS address alone.
    CallToolResult stopped = await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = server.GetProperty("server_id").GetString() });
    Console.WriteLine($"stopped: {stopped.StructuredContent!.Value.GetProperty("exit_code_name")}");
    server = (await Start(server.GetProperty("bound_urls")[1].GetString()!)).StructuredContent!.Value;
    Console.WriteLine($"bound_urls: {string.Join(" ", server.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!.Split(':')[0]))}");
}

switch (step)
{
    case "moved" or "after":
        await Moved(secure);
        break;
    case "unmoved" or "after-plain":
        await Unmoved(plain);
        break;
    case "guard":
        await Moved(plain);
        break;
    case "untrusted":
        await Curl("client without the CA certificate, https", "-sS", "--proto", "=https", secure);
        break;
    case "record":
        await Moved(secure);
        await Unmoved(plain);
        await Moved(plain);
        await Record();
        break;
    case "after-record":
        await Moved(secure);
        await Unmoved(plain);
        await Record();
        break;
}

return 0;

async Task<CallToolResult> Start(params string[] listenUrls) =>
    await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = listenUrls,
        ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key" },
        ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    });

// The moved client: the curl tool with --proto =https, trusting the test certificate with --cacert.
Task Moved(string url) => Curl($"moved client, {url.Split(':')[0]}", "-sS", "--proto", "=https", "--cacert", "server.pem", url);

async Task Curl(string who, params string[] curlArgs)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string output = run.GetProperty("exitCode").GetInt32() == 0
        ? run.GetProperty("standardOutput").GetProperty("content").GetString()!
        : run.GetProperty("standardError").GetProperty("content").GetString()!.Split('\n')[0];
    Console.WriteLine($"{who}: exitCode {run.GetProperty("exitCode")}, {output.TrimEnd()}");
}

// A client not yet moved: transfer, which takes any scheme.
async Task Unmoved(string url)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine(answer.GetProperty("failure").ValueKind == JsonValueKind.Null
        ? $"unmoved client, http: statusCode {answer.GetProperty("statusCode")}"
        : $"unmoved client, http: {answer.GetProperty("failure").GetProperty("kind")} exit {answer.GetProperty("failure").GetProperty("exitCode")}");
}

// The record: the scheme of every exchange, and those that came in over plain http.
async Task Record()
{
    Console.WriteLine($"exchanges: {await Schemes(null)}");
    Console.WriteLine($"exchanges over http: {await Schemes("http")}");
}

async Task<string> Schemes(string? scheme)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = server.GetProperty("server_id").GetString() };
    if (scheme is not null) arguments["scheme"] = scheme;
    CallToolResult result = await surl.CallToolAsync("get_exchanges", arguments);
    string[] found = result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Select(e => e.GetProperty("scheme").GetString()!).ToArray();
    return found.Length == 0 ? "none" : string.Join(" ", found);
}
