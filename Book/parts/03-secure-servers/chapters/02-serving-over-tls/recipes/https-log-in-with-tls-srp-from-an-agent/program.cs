// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a TLS-SRP test server with surl-mcp: one account and no certificate, accounts changed
// while it runs, and curl-mcp's curl tool trying to log in.
// Run: dotnet run program.cs -- <scenario>
//   fetch     the curl tool with --tlsauthtype SRP, --tlsuser and --tlspassword fetches hello.txt
//   accounts  add, change and remove TLS-SRP accounts while the server runs
//   not-srp   the same calls on a server started with --self-signed instead
//   tls13     TLS-SRP with --tlsv1.3
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args[0];

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

// surl-mcp: --tlsuser with --tlspassword is one account and implies --tlsauthtype SRP; no certificate.
string[] arguments = scenario switch
{
    "not-srp" => ["--self-signed"],
    "tls13" => ["--tlsuser", "device", "--tlspassword", "s3cret", "--tlsv1.3"],
    _ => ["--tlsuser", "device", "--tlspassword", "s3cret"],
};
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    Console.WriteLine($"exit_code: {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")})");
    Console.WriteLine(server.GetProperty("lines")[0].GetString());
    return server.GetProperty("exit_code").GetInt32();
}

string serverId = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!;

if (scenario == "fetch")
{
    // curl-mcp's curl tool with curl's TLS-SRP options; -k because there is no certificate to verify.
    CallToolResult fetched = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-k", "--tlsauthtype", "SRP", "--tlsuser", "device", "--tlspassword", "s3cret", url + "hello.txt" },
    });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode {answer.GetProperty("exitCode")}, tool error: {fetched.IsError == true}");
    foreach (string line in answer.GetProperty("standardError").GetProperty("content").GetString()!.Split('\n'))
    {
        if (line.StartsWith("Warning: --tls") || line.StartsWith("curl: ("))
        {
            Console.WriteLine(line.TrimEnd('\r'));
        }
    }

    // surl-mcp: the server's record of that connection.
    CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 5000,
    });
    Console.WriteLine($"server: exchange ended, {ended.StructuredContent!.Value.GetProperty("event").GetProperty("received_byte_count")} bytes received");
    return 1;
}

// The TLS-SRP account tools: each answers the user on success, or a tool error with a reason.
async Task Call(string what, string tool, string user, string? password = null)
{
    var fields = new Dictionary<string, object?> { ["server_id"] = serverId, ["user"] = user };
    if (password is not null)
    {
        fields["password"] = password;
    }

    CallToolResult result = await surl.CallToolAsync(tool, fields);
    Console.WriteLine($"{what}: {(result.IsError == true ? "tool error: " : "")}{result.Content.OfType<TextContentBlock>().Single().Text}");
}

await Call("add device-2", "add_tls_srp_account", "device-2", "an0ther");
await Call("add device-2 again", "add_tls_srp_account", "device-2", "other");
await Call("set device's password", "set_tls_srp_account_password", "device", "n3w-secret");
await Call("set nobody's password", "set_tls_srp_account_password", "nobody", "n3w-secret");
await Call("remove device", "remove_tls_srp_account", "device");
await Call("remove device again", "remove_tls_srp_account", "device");
return 0;
