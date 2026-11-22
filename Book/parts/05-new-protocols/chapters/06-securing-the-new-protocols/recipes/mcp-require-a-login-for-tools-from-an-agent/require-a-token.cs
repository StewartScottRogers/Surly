// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts an MCP server over TLS that lists its tools only to a client with its token
// (surl-mcp), and asks for them with and without the token (curl-mcp), over the official MCP SDK.
// Run: dotnet run require-a-token.cs -- <case>
//   token       mcps, the certificate trusted, the token sent (the default)
//   call        the same, calling read_file
//   transfer    the token as an Authorization header through the transfer tool
//   none        no token          wrong       another token
//   password    a user name and password instead of a token
//   untrusted   the token, but the certificate not trusted
//   plain       the token over plain mcp
//   plaintext   the same, with --allow-plaintext-auth
//   anonymous   mcps with --allow-anonymous, no token
//   no-cert     mcps with no certificate
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "token";
bool plain = which is "plain" or "plaintext";

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

// surl-mcp: one token (an account with an empty user name) and one password account.
var arguments = new List<string> { "--user", ":agent-token-1", "--user", "alice:secret" };
if (!plain && which != "no-cert")
{
    arguments.AddRange(["--cert", "server.pem", "--key", "server.key"]);
}

if (which == "plaintext") arguments.Add("--allow-plaintext-auth");
if (which == "anonymous") arguments.Add("--allow-anonymous");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { plain ? "mcp://127.0.0.1:0" : "mcps://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    Console.WriteLine($"exit_code: {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")})");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    return 1;
}

string url = server.GetProperty("bound_urls")[0].GetString()! + "mcp";
CallToolResult result;
if (which == "transfer")
{
    // transfer: the request as fields; the token goes in an Authorization header.
    result = await curl.CallToolAsync("transfer", new Dictionary<string, object?>
    {
        ["url"] = url, ["caFile"] = "server.pem",
        ["headers"] = JsonDocument.Parse("""[{"name":"Authorization","value":"Bearer agent-token-1"}]""").RootElement,
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement failure = answer.GetProperty("failure");
    Console.Write(failure.ValueKind == JsonValueKind.Null
        ? answer.GetProperty("body").GetProperty("content").GetString()
        : $"failure: {failure.GetProperty("kind")}: {failure.GetProperty("text")}\n");
}
else
{
    // curl: curl's own command line, as a list of arguments.
    var words = new List<string> { "-sS" };
    if (which != "untrusted" && !plain) words.AddRange(["--cacert", "server.pem"]);
    words.AddRange(which switch
    {
        "none" or "anonymous" => [],
        "wrong" => ["--oauth2-bearer", "agent-token-2"],
        "password" => ["-u", "alice:secret"],
        "call" => ["--oauth2-bearer", "agent-token-1", "-d", """{"name":"read_file","arguments":{"path":"hello.txt"}}"""],
        _ => (string[])["--oauth2-bearer", "agent-token-1"],
    });
    words.Add(url);
    result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
    Console.Write(answer.GetProperty("standardOutput").GetProperty("content").GetString());
    Console.Write(answer.GetProperty("standardError").GetProperty("content").GetString());
}

Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
