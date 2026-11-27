// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a client's login flow: it starts an HTTPS server through surl-mcp, logs in with
// Basic through curl-mcp to fetch a token, calls the API with it as a Bearer token, and reads
// back the logins the server judged.
// Run: dotnet run token-flow.cs [-- <password> [<token>]]
//   (no argument)  the right password, and the token the login hands back
//   <password>     log in with this password instead
//   <token>        call the API with this token instead of the one handed back
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string password = args.Length > 0 ? args[0] : "s3cret";
string? tokenOverride = args.Length > 1 ? args[1] : null;

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

// The server: HTTPS with the certificate in this folder, one password account, one Bearer
// token (the empty user name), and the token as a file.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "app:s3cret", "--user", ":tok-7f3a9c" },
    ["files"] = new Dictionary<string, string> { ["token.txt"] = "tok-7f3a9c\n", ["api/orders.json"] = "[{\"id\":1,\"total\":40}]\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
int exitCode = 0;

// Step 1: Basic login (curl -u names no method); the body is the token.
(int code, string output) = await Curl("-u", $"app:{password}", url + "token.txt");
Console.WriteLine($"login: curl exit {code}");
if (code != 0)
{
    exitCode = 1;
}
else
{
    // Step 2: the API, with the token as a Bearer token.
    (code, output) = await Curl("--oauth2-bearer", tokenOverride ?? output.TrimEnd(), url + "api/orders.json");
    Console.WriteLine($"api: curl exit {code} {output.TrimEnd()}".TrimEnd());
    exitCode = code == 0 ? 0 : 1;
}

// What the server recorded: every login it judged, in order.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement login in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"recorded: {login.GetProperty("mechanism")} {login.GetProperty("account")} {(login.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}

return exitCode;

// curl-mcp's curl tool over HTTPS, trusting server.pem, failing on an error status: exit code and output.
async Task<(int, string)> Curl(params string[] login)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sf", "--cacert", "server.pem" }.Concat(login).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return (answer.RootElement.GetProperty("exitCode").GetInt32(),
            answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString() ?? "");
}
