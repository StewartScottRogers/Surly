// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent stands up a test environment with surl-mcp, HTTP, FTP, SMTP and IMAP on one server,
// and smoke-tests every listener with curl-mcp before the suite would run: one request each,
// failing fast. Over the official MCP SDK.
// Run: dotnet run smoke.cs [-- all|hung|gone]
//   all   every listener of the environment (the default)
//   hung  HTTP, then a listener that accepts but never answers, then IMAP
//   gone  a URL whose server has been stopped, then FTP
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "all";

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

// The environment: four listeners on free ports over one store.
(string environment, string[] urls) = await Start(
    ["http://127.0.0.1:0", "ftp://127.0.0.1:0", "smtp://127.0.0.1:0", "imap://127.0.0.1:0"],
    ["--mail-domain", "example.com", "--mailbox", "app@example.com:secret"]);

switch (mode)
{
    case "hung":
        // An SMTP listener that holds every connection for a minute before it says anything.
        (_, string[] hung) = await Start(["smtp://127.0.0.1:0"], ["--fault", "delay;ms=60000"]);
        return await Smoke(urls[0], hung[0], urls[3]);

    case "gone":
        // A server that was started and stopped: nothing listens at its URL any more.
        (string stopped, string[] gone) = await Start(["http://127.0.0.1:0"], []);
        await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = stopped });
        return await Smoke(gone[0], urls[1]);
}

int result = await Smoke(urls);

// What the server saw: the scheme of each exchange it started.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = environment, ["kinds"] = new[] { "exchange_started" },
});
Console.WriteLine("exchanges: " + string.Join(", ", events.StructuredContent!.Value.GetProperty("events").EnumerateArray()
    .Select(e => e.GetProperty("scheme").GetString())));
return result;

// start_server on these listen URLs with these surl options; answers its ID and bound URLs.
async Task<(string, string[])> Start(string[] listenUrls, string[] options)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = listenUrls, ["arguments"] = options,
    });
    JsonElement server = started.StructuredContent!.Value;
    return (server.GetProperty("server_id").GetString()!,
        [.. server.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!)]);
}

// One quick request to each URL in turn through the curl tool, in its own protocol: a GET over
// HTTP, a NOOP over the others. A listener passes if it answers at all, a refusal included. The
// first that does not answer within 3 seconds ends the check with curl's exit code.
async Task<int> Smoke(params string[] targets)
{
    foreach (string url in targets)
    {
        string scheme = url[..url.IndexOf(':')];
        string[] ask = scheme == "http" ? [] : ["-X", "NOOP"];
        CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
        {
            ["args"] = new[] { "-s", "-m", "3", "-o", "smoke.out", "-w", "%{response_code}" }.Concat(ask).Append(url).ToArray(),
        });
        JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        int exitCode = answer.GetProperty("exitCode").GetInt32();
        string code = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
        if (exitCode != 0 && code == "000")
        {
            Console.WriteLine($"FAIL {url}: curl exit {exitCode}");
            return exitCode;
        }

        Console.WriteLine($"ok {scheme}: curl exit {exitCode}, reply {code}");
    }

    return 0;
}
