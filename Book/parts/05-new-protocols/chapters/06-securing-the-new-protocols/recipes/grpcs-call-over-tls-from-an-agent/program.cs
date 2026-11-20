// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a gRPC server over TLS with surl-mcp and calls it with curl-mcp's curl tool,
// trusting the certificate or not, with the right password or not.
// Run: dotnet run program.cs -- <case>
//   trusted          --cacert server.pem and the right password (the default)
//   untrusted        no --cacert
//   wrong-password   --cacert server.pem and a wrong password
//   in-clear         the same login to a plain grpc server
//   no-certificate   start a grpcs server with no --cert
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "trusted";

// Both hosts are confined to this folder, which holds server.pem and server.key.
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

// surl-mcp: surl's TLS options go in arguments; the files are read from SURL_MCP_ROOT.
var arguments = new List<string> { "--user", "alice:s3cret" };
if (which is not ("in-clear" or "no-certificate"))
{
    arguments.AddRange(["--cert", "server.pem", "--key", "server.key"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { which == "in-clear" ? "grpc://127.0.0.1:0" : "grpcs://127.0.0.1:0" },
    ["arguments"] = arguments,
});
if (started.IsError == true)
{
    JsonElement refused = started.StructuredContent!.Value;
    Console.WriteLine($"exit_code: {refused.GetProperty("exit_code")} ({refused.GetProperty("exit_code_name")})");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    return 1;
}

string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp: the curl tool runs curl with this command line; it answers in text, as JSON.
var curlArgs = new List<string> { "-s", "-S" };
if (which is "trusted" or "wrong-password")
{
    curlArgs.AddRange(["--cacert", "server.pem"]);
}

curlArgs.AddRange(["-u", which == "wrong-password" ? "alice:wrong" : "alice:s3cret", "-d", "{\"message\":\"hello\"}", url + "surl.test.Echo/Echo"]);
CallToolResult called = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
JsonElement answer = JsonDocument.Parse(called.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
Console.Write(answer.GetProperty("standardOutput").GetProperty("content").GetString());
string? errors = answer.GetProperty("standardError").GetProperty("content").GetString();
if (!string.IsNullOrEmpty(errors))
{
    // The first line is curl's own; what follows the code is the TLS library's or the server's words.
    Console.WriteLine(errors.Split('\n')[0].TrimEnd('\r'));
}

Console.WriteLine($"tool error: {(called.IsError == true ? "yes" : "no")}");
return called.IsError == true ? 1 : 0;
