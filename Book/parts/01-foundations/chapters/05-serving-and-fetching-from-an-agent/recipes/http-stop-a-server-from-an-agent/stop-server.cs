// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a server with surl-mcp, finds it with list_servers, stops it with stop_server,
// and checks what is left.
// Run: dotnet run stop-server.cs -- <case>
//   stop        list the running server, then stop it (the default)
//   fetch       stop it, then fetch from its URL with curl-mcp's transfer
//   stop-again  stop it, then stop the same ID again
//   exchanges   stop it, then ask for its exchanges
//   restart     stop it, then start a new server on the same URL and fetch from it
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "stop";

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

// Start a server on a free port with one file in memory, and keep its ID and URL.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
Console.WriteLine($"started {id} on {url}");

if (which == "stop") await ListServers();
await Stop(id);

CallToolResult result;
switch (which)
{
    case "fetch":
        // Nothing listens there now. curl-mcp answers in text, as JSON.
        result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt" });
        JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        JsonElement failure = answer.GetProperty("failure");
        Console.WriteLine($"statusCode: {answer.GetProperty("statusCode").GetRawText()}, failure: {failure.GetProperty("kind")}, exitCode {failure.GetProperty("exitCode")}");
        break;
    case "stop-again":
        result = await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = id });
        Console.WriteLine(result.Content.OfType<TextContentBlock>().Single().Text);
        break;
    case "exchanges":
        result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id });
        Console.WriteLine(result.Content.OfType<TextContentBlock>().Single().Text);
        break;
    case "restart":
        CallToolResult again = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
        {
            ["listen_urls"] = new[] { url },
            ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello again\n" },
        });
        string newId = again.StructuredContent!.Value.GetProperty("server_id").GetString()!;
        Console.WriteLine($"started {(newId == id ? "the same ID" : "a new ID")} on {again.StructuredContent!.Value.GetProperty("bound_urls")[0]}");
        result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt" });
        using (JsonDocument fetched = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text))
        {
            Console.Write($"fetched: {fetched.RootElement.GetProperty("body").GetProperty("content")}");
        }

        // The server's count can trail the transfer by a moment: ask until it holds the one exchange, for up to 10 s.
        for (var waited = System.Diagnostics.Stopwatch.StartNew(); await ExchangeCount(newId) < 1 && waited.Elapsed.TotalSeconds < 10;)
        {
            await Task.Delay(100);
        }

        await ListServers();
        await Stop(newId);
        break;
    default:
        return 0;
}

return result.IsError == true ? 1 : 0;

// Every server this host started that is still running.
async Task ListServers()
{
    CallToolResult listed = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
    foreach (JsonElement server in listed.StructuredContent!.Value.GetProperty("servers").EnumerateArray())
    {
        Console.WriteLine($"running: {server.GetProperty("server_id")} on {server.GetProperty("bound_urls")[0]}, exchange_count {server.GetProperty("exchange_count")}");
    }
}

// Stop a server and print the exit code surl would have exited with.
async Task Stop(string serverId)
{
    CallToolResult stopped = await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = serverId });
    JsonElement exit = stopped.StructuredContent!.Value;
    Console.WriteLine($"stopped {exit.GetProperty("server_id")}: exit_code {exit.GetProperty("exit_code")} ({exit.GetProperty("exit_code_name")})");
}

// The exchange_count list_servers gives for one server.
async Task<int> ExchangeCount(string serverId)
{
    CallToolResult listed = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
    return listed.StructuredContent!.Value.GetProperty("servers").EnumerateArray()
        .Single(server => server.GetProperty("server_id").GetString() == serverId).GetProperty("exchange_count").GetInt32();
}
