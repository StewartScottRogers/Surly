// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a surl-mcp HTTPS server from a certificate chain file and fetches from it
// through curl-mcp, trusting only the root. The certificates come from setup: in recipe.yml.
// Run: dotnet run chain.cs [-- fetch|leaf-only|no-trust|root-in-the-file|leaf-first|key-match]
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fetch";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it, and the
// certificate files lie in it.
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

// Which certificate file the server sends, and the key for the leaf at its head.
(string cert, string key) = which switch
{
    "leaf-only" => ("leaf2.pem", "leaf2.key"),
    "root-in-the-file" => ("full.pem", "leaf.key"),
    "leaf-first" => ("reversed.pem", "leaf.key"),
    "key-match" => ("chain.pem", "inter.key"),
    _ => ("chain.pem", "leaf.key"),
};
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", cert, "--key", key },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});

if (started.IsError == true)
{
    // A certificate file the server cannot use stops the start: surl's exit code 58, in structured content.
    JsonElement refusal = started.StructuredContent!.Value;
    Console.WriteLine($"start refused: {refusal.GetProperty("exit_code")} ({refusal.GetProperty("exit_code_name").GetString()})");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text.StartsWith("surl: (58) Could not load the server certificate: "));
    return 0;
}

string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

// The client trusts the root alone, or nothing. --ssl-no-revoke: the test certificates carry no
// revocation address, which Windows' Schannel would otherwise insist on.
var arguments = new List<string> { "-s", "--ssl-no-revoke" };
if (which != "no-trust") arguments.AddRange(["--cacert", "root.pem"]);
arguments.Add(url);
CallToolResult fetched = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
using JsonDocument answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text);
Console.WriteLine($"exit code: {answer.RootElement.GetProperty("exitCode").GetInt32()}");
Console.Write(answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString());
Console.WriteLine($"tool error: {(fetched.IsError == true ? "yes" : "no")}");
return 0;
