// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent rotates a shared password on a surl-mcp server that serves HTTPS and FTP: it adds the
// new account beside the old, moves each client, reads which logins still name the old account,
// and removes it last.
// Run: dotnet run rotate.cs [-- rotate|in-place|last|unknown]
//   rotate    the whole rotation (the default)
//   in-place  add_account for a name that exists
//   last      remove_account of the only account, then a GET with no login
//   unknown   remove_account of a name the server does not hold
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

// One server, HTTPS and FTP, the certificate from this folder, one account.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0", "ftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "app-v1:old-secret" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string web = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";
string ftp = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()! + "hello.txt";

switch (mode)
{
    case "in-place":
        // Adding a name that exists replaces its password at once.
        Console.WriteLine($"add_account app-v1 again: {await Account("add_account", "app-v1", "new-secret")}");
        Console.WriteLine($"https app-v1 old password: {await Web("app-v1:old-secret")}");
        Console.WriteLine($"https app-v1 new password: {await Web("app-v1:new-secret")}");
        return 0;

    case "last":
        // Removing the only account leaves no login to ask for.
        Console.WriteLine($"remove_account app-v1: {await Account("remove_account", "app-v1")}");
        Console.WriteLine($"https, no login: {await Web(null)}");
        return 0;

    case "unknown":
        Console.WriteLine($"remove_account app-v0: {await Account("remove_account", "app-v0")}");
        return 0;
}

// 1. The new password goes on a second account, beside the old one.
Console.WriteLine($"add_account app-v2: {await Account("add_account", "app-v2", "new-secret")}");

// 2. The web client moves; the FTP job has not yet. Both log in.
Console.WriteLine($"https app-v2: {await Web("app-v2:new-secret")}");
Console.WriteLine($"ftp app-v1: {await Ftp("app-v1:old-secret")}");

// 3. Who still uses the old password: the logins the server judged for app-v1.
CallToolResult found = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" }, ["account"] = "app-v1",
});
foreach (JsonElement login in found.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    string verdict = login.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused";
    Console.WriteLine($"still on app-v1: {login.GetProperty("scheme")} {login.GetProperty("mechanism")} {verdict}");
}

// 4. The FTP job moves; then the old account goes.
Console.WriteLine($"ftp app-v2: {await Ftp("app-v2:new-secret")}");
Console.WriteLine($"remove_account app-v1: {await Account("remove_account", "app-v1")}");

// 5. The old password is refused everywhere; the new one is not.
Console.WriteLine($"https app-v1: {await Web("app-v1:old-secret")}");
Console.WriteLine($"ftp app-v1: {await Ftp("app-v1:old-secret")}");
Console.WriteLine($"https app-v2: {await Web("app-v2:new-secret")}");
Console.WriteLine($"ftp app-v2: {await Ftp("app-v2:new-secret")}");
return 0;

// surl-mcp's add_account or remove_account: the structured answer, or the tool error's text.
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

// The web client: curl-mcp's curl tool, a GET over HTTPS (Basic with -u) trusting server.pem; the status code.
async Task<string> Web(string? login)
{
    string[] user = login is null ? [] : ["-u", login];
    JsonElement answer = await Curl(["-s", "-o", "hello.out", "-w", "%{http_code}", "--cacert", "server.pem", .. user, web]);
    return answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
}

// The FTP job: a download over explicit TLS (--ssl-reqd); the file, or curl's exit code.
async Task<string> Ftp(string login)
{
    JsonElement answer = await Curl(["-s", "--ssl-reqd", "--cacert", "server.pem", "-u", login, ftp]);
    int exit = answer.GetProperty("exitCode").GetInt32();
    return exit == 0 ? answer.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd() : $"exit {exit}";
}

async Task<JsonElement> Curl(string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.Clone();
}
