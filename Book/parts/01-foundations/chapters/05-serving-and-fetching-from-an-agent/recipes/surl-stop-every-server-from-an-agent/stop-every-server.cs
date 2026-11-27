// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent cleans up after itself: it lists every server its surl-mcp host runs, stops each one, and
// confirms each port is free by binding a new server to the very same URL.
// Run: dotnet run stop-every-server.cs -- <case>
//   stop  start two servers (one with two listeners), stop them all, rebind every URL (the default)
//   busy  try to bind a server's URL while that server still runs
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "stop";

// The host is confined to this folder: SURL_MCP_ROOT names it.
await using McpClient surl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl-mcp", Command = "surl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["SURL_MCP_ROOT"] = Directory.GetCurrentDirectory() },
}));

// The agent's work so far: two servers, the second with two listeners, all on free ports.
await Call("start_server", new() { ["listen_urls"] = new[] { "http://127.0.0.1:0" } });
await Call("start_server", new() { ["listen_urls"] = new[] { "http://127.0.0.1:0", "http://127.0.0.1:0" } });

// 1. What is running: every server this host started, with every URL it listens on.
JsonElement[] running = (await Call("list_servers", new())).StructuredContent!.Value.GetProperty("servers").EnumerateArray().Select(s => s.Clone()).ToArray();
string[] urls = running.SelectMany(s => s.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!)).ToArray();
foreach (JsonElement server in running)
{
    Console.WriteLine($"running: {server.GetProperty("server_id")} on {string.Join(" ", server.GetProperty("bound_urls").EnumerateArray())}");
}

if (which == "busy")
{
    // While a server runs, its URL is taken.
    CallToolResult taken = await Call("start_server", new() { ["listen_urls"] = new[] { urls[0] } });
    JsonElement refused = taken.StructuredContent!.Value;
    Console.WriteLine($"bind {urls[0]} while it runs: exit_code {refused.GetProperty("exit_code")} ({refused.GetProperty("exit_code_name")}), tool error: {(taken.IsError == true ? "yes" : "no")}");
    return 1;
}

// 2. Stop each one; each answers the exit code a surl process would have exited with.
foreach (JsonElement server in running)
{
    JsonElement stopped = (await Call("stop_server", new() { ["server_id"] = server.GetProperty("server_id").GetString() })).StructuredContent!.Value;
    Console.WriteLine($"stopped {stopped.GetProperty("server_id")}: exit_code {stopped.GetProperty("exit_code")} ({stopped.GetProperty("exit_code_name")})");
}

// 3. Confirm: nothing is listed, and every URL a stopped server held can be bound again.
Console.WriteLine($"left running: {(await Call("list_servers", new())).StructuredContent!.Value.GetProperty("servers").GetArrayLength()}");
int free = 0;
foreach (string url in urls)
{
    CallToolResult again = await Call("start_server", new() { ["listen_urls"] = new[] { url } });
    if (again.IsError != true)
    {
        free++;
        await Call("stop_server", new() { ["server_id"] = again.StructuredContent!.Value.GetProperty("server_id").GetString() });
    }
}

Console.WriteLine($"ports free: {free} of {urls.Length}");
return free == urls.Length ? 0 : 1;

// One surl-mcp tool call.
Task<CallToolResult> Call(string tool, Dictionary<string, object?> arguments) => surl.CallToolAsync(tool, arguments).AsTask();
