// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent holds a TELNET session with surl-mcp and curl-mcp, over the official MCP SDK.
// Run: dotnet run telnet-session.cs -- <case>
//   session    send hello, world and quit on stdin, and print the echo (the default)
//   upper      end the session with QUIT in capitals
//   no-quit    send one line and no quit, with a time limit
//   too-long   a server started with --max-line 10, and a line past it
//   terminal   set the terminal type and read what the server was told from get_exchanges
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "session";

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

// surl-mcp: a TELNET server on a free port. --max-line is a surl option, passed in arguments.
var start = new Dictionary<string, object?> { ["listen_urls"] = new[] { "telnet://127.0.0.1:0" } };
if (which == "too-long")
{
    start["arguments"] = new[] { "--max-line", "10" };
}

CallToolResult started = await surl.CallToolAsync("start_server", start);
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp's curl tool takes a command line and the text curl reads as standard input.
string session = which switch
{
    "upper" => "one\nQUIT\nnever read\n",
    "no-quit" => "hello\n",
    "too-long" => "short\nthis line is far too long\nnever read\nquit\n",
    _ => "hello\nworld\nquit\n",
};
var curlArgs = new List<string> { "-s" };
if (which == "no-quit")
{
    curlArgs.AddRange(["-m", "2"]);
}

if (which == "terminal")
{
    curlArgs.AddRange(["-t", "TTYPE=vt100"]);
}

curlArgs.Add(url);
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs, ["stdin"] = session });
JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
Console.Write(answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n"));
if (answer.GetProperty("failure").ValueKind != JsonValueKind.Null)
{
    Console.WriteLine($"failure: {answer.GetProperty("failure").GetProperty("kind")}");
}

if (which == "terminal")
{
    // What the client told the server is in the bytes it sent, which surl-mcp keeps: negotiation
    // as base64, the lines as text, and the two can share a chunk.
    CallToolResult seen = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
    string received = string.Concat(seen.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
        .SelectMany(e => e.GetProperty("chunks").EnumerateArray())
        .Where(c => c.GetProperty("direction").GetString() == "received")
        .Select(c => c.GetProperty("encoding").GetString() == "base64"
            ? Encoding.Latin1.GetString(Convert.FromBase64String(c.GetProperty("data").GetString()!))
            : c.GetProperty("data").GetString()));
    Console.WriteLine($"the server was told vt100: {received.Contains("vt100")}");
    Console.WriteLine($"the server read all three lines: {received.Contains("hello\nworld\nquit\n")}");
}

// The exit code says whether the last call was a tool error.
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
