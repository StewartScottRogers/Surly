// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a server on port 0 with surl-mcp and builds its URLs from the bound_urls it answers.
// Run: dotnet run bound-urls.cs -- <case>
//   fetch         two listen URLs on port 0; fetch hello.txt from each bound URL (the default)
//   list-servers  ask list_servers for the bound URLs again, as an agent that lost the answer would
//   path          a listen URL with a path, which surl refuses
#:package ModelContextProtocol.Core@*

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

// surl-mcp: a server on two free ports, one file in memory. It answers in structured content.
string[] listen = which == "path" ? ["http://127.0.0.1:0/hello.txt"] : ["http://127.0.0.1:0", "http://localhost:0"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = listen,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    // Refused as surl refuses it: surl's exit code and message, and no server to read URLs from.
    Console.WriteLine($"exit_code: {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")})");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    Console.WriteLine("tool error: yes");
    return 1;
}

Console.WriteLine($"listen_urls: {string.Join(" ", server.GetProperty("listen_urls").EnumerateArray())}");
Console.WriteLine($"bound_urls:  {string.Join(" ", server.GetProperty("bound_urls").EnumerateArray())}");

if (which == "list-servers")
{
    // The same server, found again by its ID.
    CallToolResult listed = await surl.CallToolAsync("list_servers", new Dictionary<string, object?>());
    JsonElement entry = listed.StructuredContent!.Value.GetProperty("servers").EnumerateArray()
        .Single(s => s.GetProperty("server_id").GetString() == server.GetProperty("server_id").GetString());
    Console.WriteLine($"list_servers: {string.Join(" ", entry.GetProperty("bound_urls").EnumerateArray())}");
    return 0;
}

// curl-mcp: one request to each bound URL. It answers in text, as JSON.
foreach (JsonElement bound in server.GetProperty("bound_urls").EnumerateArray())
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = $"{bound}hello.txt" });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{bound}hello.txt: {answer.GetProperty("statusCode")} {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}");
}

return 0;
