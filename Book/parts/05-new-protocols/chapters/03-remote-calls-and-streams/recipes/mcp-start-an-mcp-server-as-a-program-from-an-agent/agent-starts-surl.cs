// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's MCP client starts surl itself, as a server program over standard input and output.
// Run: dotnet run agent-starts-surl.cs -- <case>
//   call       start surl, list its tools, call read_file and echo (the default)
//   missing    call read_file for a file that is not there
//   no-login   start surl with an account and no --allow-anonymous, and list its tools
//   write      call write_file without --allow-uploads
//   upload     call write_file with --allow-uploads
//   curl-mcp   let curl-mcp's curl tool start surl from an mcp+stdio URL
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "call";
string here = Directory.GetCurrentDirectory();

if (which == "curl-mcp")
{
    // curl-mcp runs curl in-process; the mcp+stdio URL makes curl start surl and call the tool.
    await using McpClient curl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "curl-mcp", Command = "curl-mcp",
        EnvironmentVariables = new Dictionary<string, string?> { ["CURL_MCP_ROOT"] = here },
    }));
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-d", """{"name":"echo","arguments":{"text":"hello from curl-mcp"}}""",
                           "mcp+stdio://localhost/surl?--directory&site&mcp+stdio:" },
    });
    JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
    Console.Write(answer.GetProperty("standardOutput").GetProperty("content").GetString());
    return ran.IsError == true ? 1 : 0;
}

// The agent's own MCP client starts surl as its server: no port, no surl-mcp.
string[] arguments = which switch
{
    "no-login" => ["--directory", "site", "--user", "alice:secret", "mcp+stdio:"],
    "upload" => ["--directory", "site", "--allow-uploads", "mcp+stdio:"],
    _ => ["--directory", "site", "mcp+stdio:"],
};
await using McpClient surl = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl", Command = "surl", Arguments = arguments, WorkingDirectory = here,
}));
Console.WriteLine($"server: {surl.ServerInfo.Name}");

string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(t => t.Text));

try
{
    switch (which)
    {
        case "no-login":
            IList<McpClientTool> tools = await surl.ListToolsAsync();
            Console.WriteLine("tools: " + string.Join(", ", tools.Select(t => t.Name)));
            return 0;

        case "missing":
            CallToolResult missing = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["path"] = "missing.txt" });
            Console.WriteLine($"isError: {missing.IsError}, text: {Text(missing)}");
            return 1;

        case "write" or "upload":
            CallToolResult wrote = await surl.CallToolAsync("write_file", new Dictionary<string, object?>
            {
                ["path"] = "notes.txt", ["text"] = "written over stdio\n",
            });
            Console.WriteLine(Text(wrote));
            return 0;

        default:
            IList<McpClientTool> listed = await surl.ListToolsAsync();
            Console.WriteLine("tools: " + string.Join(", ", listed.Select(t => t.Name)));
            CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["path"] = "hello.txt" });
            Console.Write($"read_file: {Text(read)}");
            CallToolResult echoed = await surl.CallToolAsync("echo", new Dictionary<string, object?> { ["text"] = "hello from the agent" });
            Console.WriteLine($"echo: {Text(echoed)}");
            return 0;
    }
}
catch (McpProtocolException e)
{
    // A call the server cannot run at all is a JSON-RPC error, not a tool result.
    Console.WriteLine($"protocol error {(int)e.ErrorCode}: {e.Message}");
    return 1;
}
