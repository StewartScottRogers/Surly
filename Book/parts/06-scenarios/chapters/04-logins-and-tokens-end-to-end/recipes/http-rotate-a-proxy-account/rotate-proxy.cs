// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent rotates the account a client uses to log in to a surl-mcp proxy: it adds the new proxy
// account beside the old, moves the client, and removes the old one, proving its login is refused.
// Run: dotnet run rotate-proxy.cs [-- rotate|last|not-an-origin-account]
//   rotate                 the whole rotation (the default)
//   last                   remove_proxy_account of the only proxy account
//   not-an-origin-account  add_account (an origin account) instead of add_proxy_account
#:package ModelContextProtocol.Core@2.4.0

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

// One surl: an origin serving a file, and a proxy beside it with one proxy account.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--proxy", "http://127.0.0.1:0", "-U", "app-v1:old-secret" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string origin = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";
string proxy = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

switch (mode)
{
    case "last":
        Console.WriteLine($"remove_proxy_account app-v1: {await Account("remove_proxy_account", "app-v1")}");
        Console.WriteLine($"no proxy login: {await Through(null)}");
        Console.WriteLine($"app-v1: {await Through("app-v1:old-secret")}");
        return 0;

    case "not-an-origin-account":
        // An origin account is not a proxy account: the proxy still refuses it.
        Console.WriteLine($"add_account app-v2: {await Account("add_account", "app-v2", "new-secret")}");
        Console.WriteLine($"app-v2: {await Through("app-v2:new-secret")}");
        return 0;
}

// 1. The new password goes on a second proxy account, beside the old one.
Console.WriteLine($"add_proxy_account app-v2: {await Account("add_proxy_account", "app-v2", "new-secret")}");
Console.WriteLine($"app-v1: {await Through("app-v1:old-secret")}");
Console.WriteLine($"app-v2: {await Through("app-v2:new-secret")}");

// 2. The client has moved; the old account goes.
Console.WriteLine($"remove_proxy_account app-v1: {await Account("remove_proxy_account", "app-v1")}");
Console.WriteLine($"app-v1: {await Through("app-v1:old-secret")}");
Console.WriteLine($"app-v2: {await Through("app-v2:new-secret")}");

// 3. What the proxy judged for the old account, in order.
CallToolResult found = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" }, ["account"] = "app-v1",
});
foreach (JsonElement login in found.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    string verdict = login.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused";
    Console.WriteLine($"app-v1 login: {login.GetProperty("scheme")} {login.GetProperty("mechanism")} {verdict}");
}

return 0;

// surl-mcp's account tools: the structured answer, or the tool error's text.
async Task<string> Account(string tool, string user, string? password = null)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = id, ["user"] = user };
    if (password is not null)
    {
        arguments["password"] = password;
    }

    CallToolResult result = await surl.CallToolAsync(tool, arguments);
    return result.IsError == true
        ? $"error: {result.Content.OfType<TextContentBlock>().First().Text}"
        : result.StructuredContent!.Value.GetRawText();
}

// The client: curl-mcp's curl tool, a GET for the origin's file through the proxy, logging in to
// the proxy with Digest; the status code.
async Task<string> Through(string? login)
{
    string[] user = login is null ? [] : ["--proxy-digest", "-U", login];
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-o", "hello.out", "-w", "%{http_code}", "-x", proxy }.Concat(user).Append(origin).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
}
