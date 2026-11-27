// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent diagnoses a failed fetch with surl-mcp and curl-mcp, over the official MCP SDK.
// Run: dotnet run diagnose.cs -- <case>
//   missing      fetch a path the server does not have, with transfer and with curl -f (the default)
//   up           fetch a path it has
//   stopped      stop the server with stop_server, then fetch from it
//   unresolved   fetch from a host name that does not resolve
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "missing";

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

// surl-mcp: a server on a free port, holding one file in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string bound = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

if (which == "stopped")
{
    CallToolResult stopped = await surl.CallToolAsync("stop_server", new Dictionary<string, object?> { ["server_id"] = serverId });
    Console.WriteLine($"stop_server: tool error {(stopped.IsError == true ? "yes" : "no")}");
}

string url = which switch
{
    "missing" => bound + "missing.txt",
    "unresolved" => "http://no-such-host.invalid/hello.txt",
    _ => bound + "hello.txt",
};

// curl-mcp: the same URL two ways. transfer answers in text, as JSON, with a failure that is null when it worked.
CallToolResult transfer = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
JsonElement moved = JsonDocument.Parse(transfer.Content.OfType<TextContentBlock>().Single().Text).RootElement;
JsonElement failure = moved.GetProperty("failure");
Console.WriteLine($"transfer: statusCode {moved.GetProperty("statusCode").GetRawText()}, failure {(failure.ValueKind == JsonValueKind.Null ? "null" : failure.GetProperty("kind"))}, tool error {(transfer.IsError == true ? "yes" : "no")}");

// The curl tool runs a command line. -f makes an error status a failure, -w prints what curl knew.
CallToolResult run = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-f", "-w", "%{http_code} %{exitcode}", url },
});
JsonElement answer = JsonDocument.Parse(run.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string lastLine = answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim().Split('\n').Last().Trim();
Console.WriteLine($"curl -f: exitCode {answer.GetProperty("exitCode")}, -w [{lastLine}], tool error {(run.IsError == true ? "yes" : "no")}");
if (answer.GetProperty("failure").ValueKind != JsonValueKind.Null)
{
    Console.WriteLine($"failure: {answer.GetProperty("failure").GetProperty("text").GetString()!.Replace("\r", "")}");
}

// The exit code says whether the last call was a tool error.
return run.IsError == true ? 1 : 0;
