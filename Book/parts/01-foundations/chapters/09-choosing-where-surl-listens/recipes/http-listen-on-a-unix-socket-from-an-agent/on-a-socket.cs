// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a surl-mcp server on a Unix domain socket and fetches through it with curl-mcp's curl tool.
// Run: dotnet run on-a-socket.cs -- <case> <port>
//   fetch          serve on surl.sock, fetch hello.txt through it, read the exchange, stop (the default)
//   transfer       fetch the listen URL with the transfer tool, which has no socket field
//   file-exists    start a server on a path where a file already is
//   outside-root   start a server on a socket path outside SURL_MCP_ROOT
//   curl-outside   ask the curl tool for a socket path outside CURL_MCP_ROOT
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fetch";
string listenUrl = $"http://localhost:{(args.Length > 1 ? args[1] : "8080")}";

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

// surl-mcp: one listen URL, and surl's --unix-socket among the arguments. The path is inside the root.
// Each scenario has its own socket file: a host that ends without stop_server leaves its file behind.
string socket = which switch { "fetch" => "surl.sock", "file-exists" => "taken.sock", "outside-root" => "../surl.sock", _ => $"{which}.sock" };
CallToolResult result = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { listenUrl },
    ["arguments"] = new[] { "--unix-socket", socket },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello over a socket\n" },
});
JsonElement started = result.StructuredContent!.Value;
if (result.IsError == true)
{
    if (started.TryGetProperty("exit_code", out JsonElement code))
    {
        Console.WriteLine($"exit_code: {code} ({started.GetProperty("exit_code_name")})");
    }

    Console.WriteLine(result.Content.OfType<TextContentBlock>().Single().Text);
    if (which == "file-exists")
    {
        Console.WriteLine($"{socket} still holds: {File.ReadAllText(socket).TrimEnd()}");
    }
}
else
{
    Console.WriteLine(started.GetProperty("log_lines")[0].GetString());
    string url = started.GetProperty("bound_urls")[0].GetString()! + "hello.txt";
    if (which == "transfer")
    {
        // transfer describes a request by fields, and none of them is a socket: it connects over TCP.
        result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
        JsonElement failure = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("failure");
        Console.WriteLine($"failure: {failure.GetProperty("kind")} {failure.GetProperty("exitCode")}");
    }
    else
    {
        // curl-mcp's curl tool takes curl's own --unix-socket; it checks the path against its root.
        string through = which == "curl-outside" ? "../surl.sock" : socket;
        result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
        {
            ["args"] = new[] { "-s", "--unix-socket", through, "-w", "%{http_code}\n", url },
        });
        JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        if (answer.GetProperty("failure").ValueKind != JsonValueKind.Null)
        {
            Console.WriteLine($"failure: {answer.GetProperty("failure").GetProperty("kind")}");
        }
        else
        {
            Console.Write(answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n"));

            // What the server saw: one exchange, from a client with no address.
            CallToolResult seen = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = "s1" });
            JsonElement exchange = seen.StructuredContent!.Value.GetProperty("exchanges")[0];
            Console.WriteLine($"client: {exchange.GetProperty("client")}");
            string head = exchange.GetProperty("chunks")[0].GetProperty("data").GetString()!;
            Console.WriteLine(head.Split("\r\n").First(line => line.StartsWith("Host:")));

            // Stopping the server removes the socket file it made.
            CallToolResult stopped = await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = "s1" });
            Console.WriteLine($"stopped: {stopped.StructuredContent!.Value.GetProperty("exit_code_name")}, socket file there: {File.Exists(socket)}");
        }
    }
}

// The exit code says whether the last call was a tool error.
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
