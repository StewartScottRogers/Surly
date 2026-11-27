// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an origin and a SOCKS5 server with surl-mcp, fetches through it with curl-mcp, and
// reads the SOCKS request and reply back from the exchange the server recorded.
// Run: dotnet run socks.cs -- <case>
//   fetch        transfer through socks5h://: the server resolves localhost (the default)
//   local        the curl tool through socks5://, resolving origin.test itself with --resolve
//   remote-name  the same through socks5h://: the server cannot resolve the name
//   socks4       transfer through socks4://
//   outside      transfer to an origin outside loopback, 192.0.2.1
//   no-login     a server with an account, and no login in the proxy URL
//   in-clear     the same server and the right login in the proxy URL
//   plaintext    the same, to a server started with --allow-plaintext-auth
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
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

// One server: the origin on localhost and a SOCKS5 server; -U gives every proxy listener an account.
List<string> arguments = which switch
{
    "no-login" or "in-clear" => ["-U", "app:socks-pw"],
    "plaintext" => ["-U", "app:socks-pw", "--allow-plaintext-auth"],
    _ => [],
};
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://localhost:0", "socks5h://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string origin = server.GetProperty("bound_urls")[0].GetString()!;
string socks = server.GetProperty("bound_urls")[1].GetString()!.TrimEnd('/');
int port = new Uri(origin).Port;

bool relayed;
if (which is "local" or "remote-name")
{
    // transfer has no field for --resolve: the curl tool takes curl's own words.
    string scheme = which == "local" ? "socks5" : "socks5h";
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "--max-time", "20", "--resolve", $"origin.test:{port}:127.0.0.1", "-x", socks.Replace("socks5h", scheme), $"http://origin.test:{port}/hello.txt" },
    });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string output = run.GetProperty("exitCode").GetInt32() == 0
        ? run.GetProperty("standardOutput").GetProperty("content").GetString()! : run.GetProperty("standardError").GetProperty("content").GetString()!;
    Console.WriteLine($"curl tool: exitCode {run.GetProperty("exitCode")}, {output.TrimEnd()}");
    relayed = run.GetProperty("exitCode").GetInt32() == 0;
}
else
{
    // curl-mcp transfer: proxy takes the scheme, and a login as user:password@ in the URL.
    string proxy = which switch
    {
        "socks4" => socks.Replace("socks5h", "socks4"),
        "in-clear" or "plaintext" => socks.Replace("socks5h://", "socks5h://app:socks-pw@"),
        _ => socks,
    };
    string url = which == "outside" ? "http://192.0.2.1/hello.txt" : origin + "hello.txt";
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url, ["proxy"] = proxy, ["timeoutSeconds"] = 20 });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement failure = answer.GetProperty("failure");
    Console.WriteLine(failure.ValueKind == JsonValueKind.Null
        ? $"transfer: statusCode {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}"
        : $"transfer: exitCode {failure.GetProperty("exitCode")}, {failure.GetProperty("text").GetString()!.Split('\n')[0]}, tool error: {(fetched.IsError == true ? "yes" : "no")}");
    relayed = failure.ValueKind == JsonValueKind.Null;
}

// surl-mcp get_exchanges: the SOCKS exchange's chunks, base64 when they are not text. The record can
// trail curl's answer by a moment: ask again until each SOCKS message has its reply, and a relayed
// request is there when the fetch worked, for at most 10 s.
JsonElement recorded = default;
for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); ; await Task.Delay(100))
{
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = server.GetProperty("server_id").GetString(), ["listen_url"] = socks + "/",
    });
    recorded = exchanges.StructuredContent!.Value.GetProperty("exchanges").Clone();
    bool settled = recorded.EnumerateArray().All(x =>
    {
        var c = x.GetProperty("chunks").EnumerateArray().ToList();
        int last = c.FindLastIndex(k => k.GetProperty("direction").GetString() == "received" && k.GetProperty("encoding").GetString() != "text");
        return c.Skip(last + 1).Any(k => k.GetProperty("direction").GetString() == "sent")
            && (!relayed || c.Any(k => k.GetProperty("encoding").GetString() == "text" && k.GetProperty("data").GetString()!.StartsWith("GET /hello.txt")));
    });
    if (settled || DateTime.UtcNow >= deadline) break;
}

foreach (JsonElement exchange in recorded.EnumerateArray())
{
    var chunks = exchange.GetProperty("chunks").EnumerateArray()
        .Select(c => (Sent: c.GetProperty("direction").GetString() == "sent", Text: c.GetProperty("encoding").GetString() == "text",
            Bytes: c.GetProperty("encoding").GetString() == "base64" ? Convert.FromBase64String(c.GetProperty("data").GetString()!) : Encoding.UTF8.GetBytes(c.GetProperty("data").GetString()!)))
        .ToList();
    // The request is the received chunk that starts 05 01 (SOCKS5 CONNECT) or 04 (SOCKS4); its reply is the next sent chunk.
    int at = chunks.FindIndex(c => !c.Sent && !c.Text && (c.Bytes[0] == 4 || (c.Bytes.Length > 4 && c.Bytes[0] == 5 && c.Bytes[1] == 1)));
    if (at < 0)
    {
        Console.WriteLine($"no SOCKS request; the server's last reply: {Convert.ToHexString(chunks.Last(c => c.Sent).Bytes)}");
        continue;
    }
    byte[] request = chunks[at].Bytes;
    string asked = request[0] == 4 ? "a SOCKS4 request"
        : request[3] == 3 ? $"a name, {Encoding.ASCII.GetString(request, 5, request[4])}"
        : request[3] == 1 ? $"an address, {string.Join('.', request[4..8])}" : $"address type {request[3]}";
    byte[] reply = chunks.Skip(at + 1).First(c => c.Sent).Bytes;
    Console.WriteLine($"asked for {asked}; replied {reply[0]:X2} {reply[1]:X2}");
    Console.WriteLine($"relayed in clear: {(chunks.Any(c => !c.Sent && c.Text && Encoding.UTF8.GetString(c.Bytes).StartsWith("GET /hello.txt")) ? "GET /hello.txt" : "nothing")}");
}
return 0;
