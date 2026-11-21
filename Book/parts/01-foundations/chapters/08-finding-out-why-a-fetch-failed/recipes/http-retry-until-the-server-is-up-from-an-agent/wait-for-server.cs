// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's first transfer is refused because no server is up; it starts one with surl-mcp on
// that port and retries, a bounded number of times. Run: dotnet run wait-for-server.cs -- <case> [tries]
//   start   start the server after the first refusal (the default)
//   never   never start it, and give up after the tries
//   curl    the curl tool with --retry 5 --retry-connrefused --retry-delay 1, the server started
//           two seconds into it
//   tries   how many transfers the agent may make in all (3 if not given)
#:package ModelContextProtocol.Core@*

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "start";
int tries = args.Length > 1 ? int.Parse(args[1]) : 3;

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

// The URL the agent was told to fetch: a free port, where nothing listens yet.
var probe = new TcpListener(IPAddress.Loopback, 0);
probe.Start();
int port = ((IPEndPoint)probe.LocalEndpoint).Port;
probe.Stop();
string url = $"http://127.0.0.1:{port}/hello.txt";

if (which == "curl")
{
    // The curl tool runs a curl command line in-process, retrying on its own while the agent starts the server.
    Task<CallToolResult> running = curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "--retry", "5", "--retry-connrefused", "--retry-delay", "1", "-w", "%{num_retries}\n", url },
    }).AsTask();
    await Task.Delay(TimeSpan.FromSeconds(2));
    await StartServer();
    JsonElement ran = JsonDocument.Parse((await running).Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string output = ran.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "");
    Console.WriteLine($"curl: exit {ran.GetProperty("exitCode")}, {output.Replace("\n", " | ").TrimEnd(' ', '|')} retries");
    return 0;
}

for (int attempt = 1; attempt <= tries; attempt++)
{
    // transfer answers in text, as JSON: a status, or a failure carrying curl's exit code.
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement failure = answer.GetProperty("failure");
    if (failure.ValueKind == JsonValueKind.Null)
    {
        Console.Write($"transfer {attempt}: {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()}");
        return 0;
    }

    Console.WriteLine($"transfer {attempt}: curl exit {failure.GetProperty("exitCode")}, tool error: {(fetched.IsError == true ? "yes" : "no")}");
    if (failure.GetProperty("exitCode").GetInt32() != 7)
    {
        return 1;   // not a refused connection: starting a server would not help
    }

    if (attempt == 1 && which == "start")
    {
        // Refused: nothing listens there. Start the server on that very port.
        await StartServer();
    }
    else if (attempt < tries)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}

Console.WriteLine($"gave up after {tries} transfers");
return 7;

// start_server on the agreed port, with the file in memory; it answers the URL it bound.
async Task StartServer()
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { $"http://127.0.0.1:{port}" },
        ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
    });
    string bound = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
    Console.WriteLine($"start_server: {(bound == $"http://127.0.0.1:{port}/" ? "the same port" : bound)}, tool error: {(started.IsError == true ? "yes" : "no")}");
}
