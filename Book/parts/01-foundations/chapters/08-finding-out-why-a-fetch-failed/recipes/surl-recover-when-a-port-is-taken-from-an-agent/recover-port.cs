// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent asks surl-mcp for a fixed port; if start_server is refused because the port is taken,
// it reads the exit code and starts again on port 0. Run: dotnet run recover-port.cs -- [case]
//   taken   another listener holds the port first (the default)
//   free    nothing holds it
//   bad     the port is 70000, which is no port at all
#:package ModelContextProtocol.Core@*

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "taken";

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

// The fixed port the agent was told to use. In "taken", something else, a plain TcpListener, holds it.
var holder = new TcpListener(IPAddress.Loopback, 0);
holder.Start();
int port = which == "bad" ? 70000 : ((IPEndPoint)holder.LocalEndpoint).Port;
if (which != "taken")
{
    holder.Stop();
}

CallToolResult started = await Start(port);
if (started.IsError == true)
{
    // A refused start is a tool error carrying surl's exit code, its name, and surl's message as text.
    JsonElement refusal = started.StructuredContent!.Value;
    Console.WriteLine($"start_server: tool error, exit_code {refusal.GetProperty("exit_code")} ({refusal.GetProperty("exit_code_name")})");
    Console.WriteLine($"  {started.Content.OfType<TextContentBlock>().Single().Text}");
    if (refusal.GetProperty("exit_code").GetInt32() != 45)
    {
        return 1;   // not a taken port: port 0 would not fix it
    }

    // 45, BindFailed: start again on port 0; bound_urls says which port the system chose.
    started = await Start(0);
}

string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
int bound = new Uri(url).Port;
Console.WriteLine($"start_server: bound {(bound == port ? "the port asked for" : "another port, " + (bound > 0 ? "not 0" : "0"))}");

// The server is up wherever it landed: fetch the file from the URL it bound. curl-mcp answers in text, as JSON.
CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt" });
JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.Write($"transfer: {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()}");
holder.Stop();
return 0;

// start_server on the loopback address and the given port, with one file in memory.
Task<CallToolResult> Start(int port) => surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { $"http://127.0.0.1:{port}" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
}).AsTask();
