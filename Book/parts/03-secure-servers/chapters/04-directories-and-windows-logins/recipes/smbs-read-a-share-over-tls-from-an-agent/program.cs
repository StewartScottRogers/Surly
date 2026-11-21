// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side of the conversation, as a C# file-based app: `dotnet run program.cs -- trusted`
// (also: untrusted, wrong-password, no-certificate, self-signed, self-signed-skip). It starts surl-mcp
// and curl-mcp over standard input and output with the official MCP SDK and calls their tools.
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args[0];

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it, and server.pem and server.key are in it.
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

// surl-mcp: start_server has no TLS fields, so the certificate goes in as surl's own words.
var serverOptions = new List<string> { "-u", "alice:s3cret", "--auth", "ntlmv1" };
if (mode.StartsWith("self-signed")) serverOptions.Add("--self-signed");
else if (mode != "no-certificate") serverOptions.AddRange(["--cert", "server.pem", "--key", "server.key"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smbs://127.0.0.1:0" },
    ["arguments"] = serverOptions,
    ["files"] = new Dictionary<string, string> { ["docs/readme.txt"] = "read me first\n" },
});
if (started.IsError == true)
{
    // A refusal is a tool error: surl's line as text, and its exit code in the structured content.
    JsonElement refusal = started.StructuredContent!.Value;
    Console.WriteLine($"start: refused, exit {refusal.GetProperty("exit_code").GetInt32()} {refusal.GetProperty("exit_code_name").GetString()}");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().First().Text.Trim());
    return 0;
}
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "docs/readme.txt";

// curl-mcp: the curl tool takes curl's own arguments, so --cacert and -k are there; transfer has only insecure.
var curlArgs = new List<string> { "-s", "-S", "-u", mode == "wrong-password" ? "alice:guess" : "alice:s3cret" };
if (mode is "trusted" or "wrong-password") curlArgs.AddRange(["--cacert", "server.pem"]);
if (mode == "self-signed-skip") curlArgs.Add("-k");
curlArgs.Add(url);
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
JsonElement root = answer.RootElement;
string text = Content(root, "standardOutput") ?? Content(root, "standardError") ?? "";
Console.WriteLine($"read: exit {root.GetProperty("exitCode").GetInt32()} {text.Trim()}".TrimEnd());
return 0;

static string? Content(JsonElement root, string stream) =>
    root.GetProperty(stream).ValueKind == JsonValueKind.Object && root.GetProperty(stream).GetProperty("content").GetString() is { Length: > 0 } s ? s : null;
