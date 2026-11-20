// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's earlier work left servers running on surl-mcp; the agent lists them, stops each one
// and checks that none is left.
// Run: dotnet run clean-up.cs -- <case>
//   clean       list the leftovers, stop each one, list again (the default)
//   fetch       clean up, then fetch from a URL a leftover served
//   stop-again  clean up, then stop one of the same IDs again
//   other-host  ask a second surl-mcp host for its servers while the first still runs three
//   host-exit   start a server on a second host, end that host, then fetch from the server
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "clean";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();
await using McpClient surl = await StartSurlMcp();
await using McpClient curl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "curl-mcp", Command = "curl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["CURL_MCP_ROOT"] = here },
}));

// The earlier work: three servers the agent started and never stopped.
for (int i = 0; i < 3; i++)
{
    await surl.CallToolAsync("start_server", new Dictionary<string, object?> { ["listen_urls"] = new[] { "http://127.0.0.1:0" } });
}

if (which == "other-host")
{
    await using McpClient second = await StartSurlMcp();
    Console.WriteLine($"first host runs {(await ListServers(surl)).Length}; a second host runs {(await ListServers(second)).Length}");
    return 0;
}

if (which == "host-exit")
{
    string url;
    await using (McpClient second = await StartSurlMcp())
    {
        CallToolResult started = await second.CallToolAsync("start_server", new Dictionary<string, object?> { ["listen_urls"] = new[] { "http://127.0.0.1:0" } });
        url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
        Console.WriteLine($"second host started a server on {url}, then ended");
    }

    return await Fetch(url);
}

// The clean-up: list what is running, stop each one, and list again.
JsonElement[] leftovers = await ListServers(surl);
foreach (JsonElement server in leftovers)
{
    Console.WriteLine($"running: {server.GetProperty("server_id")} on {server.GetProperty("bound_urls")[0]}");
}

foreach (JsonElement server in leftovers)
{
    CallToolResult stopped = await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = server.GetProperty("server_id").GetString() });
    JsonElement exit = stopped.StructuredContent!.Value;
    Console.WriteLine($"stopped {exit.GetProperty("server_id")}: exit_code {exit.GetProperty("exit_code")} ({exit.GetProperty("exit_code_name")})");
}

Console.WriteLine($"left running: {(await ListServers(surl)).Length}");

switch (which)
{
    case "fetch":
        return await Fetch(leftovers[0].GetProperty("bound_urls")[0].GetString()!);
    case "stop-again":
        CallToolResult again = await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = leftovers[0].GetProperty("server_id").GetString() });
        Console.WriteLine(again.Content.OfType<TextContentBlock>().Single().Text);
        Console.WriteLine($"tool error: {(again.IsError == true ? "yes" : "no")}");
        return again.IsError == true ? 1 : 0;
    default:
        return 0;
}

// Every server a surl-mcp host started and still runs.
static async Task<JsonElement[]> ListServers(McpClient host)
{
    CallToolResult listed = await host.CallToolAsync("list_servers", new Dictionary<string, object?>());
    return listed.StructuredContent!.Value.GetProperty("servers").EnumerateArray().Select(s => s.Clone()).ToArray();
}

// A fetch with curl-mcp's transfer; it answers in text, as JSON.
async Task<int> Fetch(string url)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement failure = answer.GetProperty("failure");
    Console.WriteLine(failure.ValueKind == JsonValueKind.Null
        ? $"statusCode: {answer.GetProperty("statusCode")}"
        : $"statusCode: null, failure: {failure.GetProperty("kind")}, exitCode {failure.GetProperty("exitCode")}");
    return result.IsError == true ? 1 : 0;
}

Task<McpClient> StartSurlMcp() => McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl-mcp", Command = "surl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["SURL_MCP_ROOT"] = here },
}));
