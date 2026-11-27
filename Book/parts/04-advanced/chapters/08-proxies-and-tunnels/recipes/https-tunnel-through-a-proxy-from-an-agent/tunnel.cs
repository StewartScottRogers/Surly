// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a TLS origin and a proxy with surl-mcp, fetches the origin's https:// URL through a
// CONNECT tunnel with curl-mcp, and reads back what the proxy and the origin received.
// Run: dotnet run tunnel.cs -- <case>
//   fetch        fetch hello.txt through the http proxy, then read the exchanges (the default)
//   untrusted    the same fetch without caFile
//   https-proxy  through the https proxy: transfer, then the curl tool with --proxy-cacert
//   refused      through a proxy whose fault rule answers CONNECT with 503
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

// One server: the TLS origin on the listen URL, an http and an https proxy listener from arguments.
List<string> arguments = ["--proxy", "http://127.0.0.1:0", "--proxy", "https://127.0.0.1:0", "--cert", "server.pem", "--key", "server.key"];
if (which == "refused") arguments.AddRange(["--fault", "error;scheme=http-proxy;command=CONNECT;reply=503"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string origin = server.GetProperty("bound_urls")[0].GetString()!;
string httpProxy = server.GetProperty("bound_urls")[1].GetString()!;
string httpsProxy = server.GetProperty("bound_urls")[2].GetString()!;
string file = origin + "hello.txt";

// curl-mcp transfer: an https:// URL through a proxy goes in a CONNECT tunnel. It answers in text, as JSON.
async Task Transfer(string proxy, string? caFile)
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = file, ["proxy"] = proxy, ["caFile"] = caFile });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement failure = answer.GetProperty("failure");
    Console.WriteLine(failure.ValueKind == JsonValueKind.Null
        ? $"statusCode: {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}"
        : $"failure: exitCode {failure.GetProperty("exitCode")}, {failure.GetProperty("text").GetString()!.Split('\n')[^1]}, tool error: {(fetched.IsError == true ? "yes" : "no")}");
}

switch (which)
{
    case "fetch":
    {
        await Transfer(httpProxy, "server.pem");
        // surl-mcp get_exchanges: each chunk is text, or base64 when it is not. The record can trail
        // curl's answer by a moment: ask again until the proxy and the origin have each sent something,
        // for at most 10 s.
        JsonElement recorded = default;
        for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); ; await Task.Delay(100))
        {
            CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server.GetProperty("server_id").GetString() });
            recorded = exchanges.StructuredContent!.Value.GetProperty("exchanges").Clone();
            bool Answered(string listener) => recorded.EnumerateArray().Any(x => x.GetProperty("listen_url").GetString() == listener
                && x.GetProperty("chunks").EnumerateArray().Any(c => c.GetProperty("direction").GetString() == "sent"));
            if ((Answered(httpProxy) && Answered(origin)) || DateTime.UtcNow >= deadline) break;
        }

        foreach (JsonElement exchange in recorded.EnumerateArray())
        {
            string listener = exchange.GetProperty("listen_url").GetString() == httpProxy ? "proxy" : "origin";
            var chunks = exchange.GetProperty("chunks").EnumerateArray().ToList();
            string Text(JsonElement c) => c.GetProperty("encoding").GetString() == "base64"
                ? Encoding.Latin1.GetString(Convert.FromBase64String(c.GetProperty("data").GetString()!)) : c.GetProperty("data").GetString()!;
            string First(string direction) => Text(chunks.First(c => c.GetProperty("direction").GetString() == direction)).Split("\r\n")[0];
            string received = string.Concat(chunks.Where(c => c.GetProperty("direction").GetString() == "received").Select(Text));
            Console.WriteLine($"{listener} ({exchange.GetProperty("scheme").GetString()}) received: {First("received")} | sent: {First("sent")} | GET /hello.txt in clear: {(chunks.Any(c => c.GetProperty("encoding").GetString() == "text" && Text(c).Contains("GET /hello.txt")) ? "yes" : "no")}");
            if (listener == "proxy")
                Console.WriteLine($"proxy chunks after the CONNECT: {chunks.Skip(2).Count(c => c.GetProperty("encoding").GetString() == "base64")} of {chunks.Count - 2} in base64");
        }
        break;
    }
    case "untrusted":
        await Transfer(httpProxy, null);
        break;
    case "https-proxy":
    {
        // transfer's caFile is the origin's; it has no field for the proxy's certificate.
        await Transfer(httpsProxy, "server.pem");
        CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
        {
            ["args"] = new[] { "-s", "--proxy-cacert", "server.pem", "--cacert", "server.pem", "-x", httpsProxy, file },
        });
        JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        Console.WriteLine($"curl tool: exitCode {run.GetProperty("exitCode")}, {run.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd()}");
        break;
    }
    case "refused":
        await Transfer(httpProxy, "server.pem");
        break;
}
return 0;
