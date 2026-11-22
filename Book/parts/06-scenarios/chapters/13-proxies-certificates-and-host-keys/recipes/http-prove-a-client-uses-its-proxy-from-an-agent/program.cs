// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent proves a client went through its proxy: surl-mcp serves the origin and the proxy, curl-mcp's
// transfer tool is the client, and get_exchanges shows whether each request the origin got carries Via.
// Run: dotnet run program.cs -- <scenario>
//   proxied  the client fetches hello.txt and prices.json through the proxy
//   bypass   it fetches hello.txt through the proxy, then once more without it
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args[0];

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

// surl-mcp: the origin is the listen URL, and --proxy in arguments adds the proxy listener.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--proxy", "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n", ["prices.json"] = "{\"widget\": 3}\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string origin = server.GetProperty("bound_urls")[0].GetString()!, proxy = server.GetProperty("bound_urls")[1].GetString()!;

// curl-mcp's transfer tool is the client; its proxy field is curl -x.
async Task Fetch(string path, bool useProxy)
{
    var fields = new Dictionary<string, object?> { ["url"] = origin + path };
    if (useProxy)
    {
        fields["proxy"] = proxy;
    }

    CallToolResult result = await curl.CallToolAsync("transfer", fields);
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string via = answer.GetProperty("headers").EnumerateArray()
        .Where(h => h.GetProperty("name").GetString()!.Equals("Via", StringComparison.OrdinalIgnoreCase))
        .Select(h => h.GetProperty("value").GetString()).FirstOrDefault() ?? "none";
    Console.WriteLine($"{path}: {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("totalBytes")} bytes, Via: {via}");
}

if (scenario == "proxied")
{
    await Fetch("hello.txt", true);
    await Fetch("prices.json", true);
}
else
{
    await Fetch("hello.txt", true);
    await Fetch("hello.txt", false);
}

// The test: every request head a listener received, from get_exchanges with listen_url. Text chunks
// that were received are joined, then split at the blank line that ends each head (GET has no body).
async Task<List<string>> Heads(string listener)
{
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["listen_url"] = listener,
    });
    return exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .Select(e => string.Concat(e.GetProperty("chunks").EnumerateArray()
            .Where(c => c.GetProperty("direction").GetString() == "received")
            .Select(c => c.GetProperty("data").GetString())))
        .SelectMany(text => text.Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries))
        .ToList();
}

int forwarded = (await Heads(proxy)).Count(h => h.StartsWith($"GET {origin}"));
List<string> atOrigin = await Heads(origin);
int proxied = atOrigin.Count(h => h.Contains("\r\nVia: 1.1 surl"));
Console.WriteLine($"proxy received {forwarded} requests for the origin");
Console.WriteLine($"origin: {proxied} via the proxy, {atOrigin.Count - proxied} direct");
return atOrigin.Count == proxied ? 0 : 1;
