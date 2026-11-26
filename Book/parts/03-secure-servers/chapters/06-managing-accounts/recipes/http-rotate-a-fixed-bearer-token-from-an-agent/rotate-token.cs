// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent adds, replaces and removes the fixed Bearer token of a surl-mcp HTTPS server, the
// account whose user name is empty, and checks after each step which tokens it accepts.
// Run: dotnet run rotate-token.cs [-- rotate|refusals]
//   rotate    add, replace and remove the token (the default)
//   refusals  a second add_account, then set_account_password and remove_account with no token set
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "rotate";

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

// An HTTPS server, the certificate from this folder, no account.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

if (mode == "refusals")
{
    await Account("add_account", "token-one");
    Console.WriteLine($"add_account again: {await Account("add_account", "token-two")}");
    Console.WriteLine($"token-one: {await Get("token-one")}");
    await Account("remove_account");
    Console.WriteLine($"remove_account, none set: {await Account("remove_account")}");
    Console.WriteLine($"set_account_password, none set: {await Account("set_account_password", "token-two")}");
    return 0;
}

// 1. With no account the server needs no login. Adding the token closes it.
Console.WriteLine($"no token, no account: {await Get(null)}");
Console.WriteLine($"add_account: {await Account("add_account", "token-one")}");
Console.WriteLine($"no token: {await Get(null)}");
Console.WriteLine($"token-one: {await Get("token-one")}");

// 2. Replace it: the old token is refused from the next request on.
Console.WriteLine($"set_account_password: {await Account("set_account_password", "token-two")}");
Console.WriteLine($"token-one: {await Get("token-one")}");
Console.WriteLine($"token-two: {await Get("token-two")}");

// 3. What the server judged: every token login is the account "bearer token", never the token.
CallToolResult found = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement login in found.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    string verdict = login.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused";
    Console.WriteLine($"judged: {login.GetProperty("mechanism")} {login.GetProperty("account")} {verdict}");
}

// 4. Remove it: the old token is refused, but the server, with no account left, needs no login.
Console.WriteLine($"remove_account: {await Account("remove_account")}");
Console.WriteLine($"token-two: {await Get("token-two")}");
Console.WriteLine($"no token: {await Get(null)}");
return 0;

// add_account, set_account_password or remove_account for the empty user name, the Bearer-token
// account: the structured answer, or the tool error's text.
async Task<string> Account(string tool, string? token = null)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = id, ["user"] = "" };
    if (token is not null)
    {
        arguments["password"] = token;
    }

    CallToolResult result = await surl.CallToolAsync(tool, arguments);
    return result.IsError == true
        ? $"error: {result.Content.OfType<TextContentBlock>().First().Text}"
        : result.StructuredContent!.Value.GetRawText();
}

// curl-mcp's curl tool: a GET over HTTPS trusting server.pem, with the token as a Bearer header
// (--oauth2-bearer) or with none; the status code.
async Task<string> Get(string? token)
{
    string[] bearer = token is null ? [] : ["--oauth2-bearer", token];
    string[] arguments = ["-s", "-o", "hello.out", "-w", "%{http_code}", "--cacert", "server.pem", .. bearer, url];
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
}
