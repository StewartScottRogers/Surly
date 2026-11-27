// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent asks an XMPP server what it offers: surl-mcp starts the server and reports what it saw,
// curl-mcp asks with service discovery. Run: dotnet run discover.cs -- <case>
//   solution        the server's own disco info
//   account         the disco info of an account
//   items-not-asked ?disco;items, and the question curl really sent
//   stanza-errors   an unknown node, an account with no stream, a foreign domain
//   login           no login, and a wrong password
//   untrusted       no --cacert
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. server.pem and
// server.key, made by `openssl` in setup, are in it.
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

// An XMPP server with a certificate and two accounts: TLS is required before a login.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "xmpp://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "alice:s3cret", "--user", "bob:hunter2" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "solution":
        await Ask("--cacert", "server.pem", "-u", "alice:s3cret", url + "127.0.0.1");
        break;
    case "account":
        await Ask("--cacert", "server.pem", "-u", "alice:s3cret", url + "alice@127.0.0.1");
        break;
    case "items-not-asked":
        await Ask("--cacert", "server.pem", "-u", "alice:s3cret", url + "127.0.0.1?disco;items");
        await Questions();
        break;
    case "stanza-errors":
        await Ask("--cacert", "server.pem", "-u", "alice:s3cret", url + "127.0.0.1?disco;node=x");
        await Ask("--cacert", "server.pem", "-u", "alice:s3cret", url + "bob@127.0.0.1");
        await Ask("--cacert", "server.pem", "-u", "alice:s3cret", url + "example.com");
        break;
    case "login":
        await Ask("--cacert", "server.pem", url + "127.0.0.1");
        await Ask("--cacert", "server.pem", "-u", "alice:wrong", url + "127.0.0.1");
        break;
    case "untrusted":
        await Ask("-u", "alice:s3cret", url + "127.0.0.1");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// The curl tool runs curl's own arguments. With no -d, an xmpp: URL asks service discovery, so
// curl writes the answer. The text of a certificate failure is the TLS
// library's and differs by platform, so it is reduced to curl's exit code.
async Task Ask(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = arguments.Prepend("-sS").ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    int exit = root.GetProperty("exitCode").GetInt32();
    string output = root.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();
    string error = root.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    string said = exit == 60 ? "certificate not trusted" : exit == 0 ? output : error;
    Console.WriteLine($"curl exit {exit}, tool error {(result.IsError == true ? "yes" : "no")}: {said}");
}

// get_exchanges answers what each client sent and what the server sent back, as text. The program
// prints the IQ questions curl asked, from the chunks the server received.
async Task Questions()
{
    // The server records the last bytes of an exchange a moment after curl has its answer: ask again
    // until a question is there, for at most 10 s.
    List<string> asked = await Asked();
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); asked.Count == 0 && DateTime.UtcNow < deadline; asked = await Asked())
    {
        await Task.Delay(100);
    }

    foreach (string data in asked)
    {
        Console.WriteLine($"curl asked: {data}");
    }
}

async Task<List<string>> Asked()
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
    return result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(exchange => exchange.GetProperty("chunks").EnumerateArray())
        .Where(chunk => chunk.GetProperty("direction").GetString() == "received" && chunk.GetProperty("data").GetString()!.Contains("disco#"))
        .Select(chunk => chunk.GetProperty("data").GetString()!)
        .ToList();
}
