// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent adds and removes accounts on a running surl server through surl-mcp, and logs in
// with curl through curl-mcp after each change. Run: dotnet run accounts.cs -- <case>
//   solution         bob refused, added, let in, removed, refused again
//   wrong-password   an added account with the wrong password
//   no-overwrite     add_account for a user who already has an account
//   change-password  remove_account, then add_account with a new password
//   no-colon         a user name holding ':'
//   basic-in-clear   the same login sent as Basic over plain HTTP
//   no-such-account  remove_account twice, and on a server that is not running
//   last-account     remove_account on alice, the account given at start
//   first-account    add_account on a server that has no accounts left
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// A plain HTTP server on a free port, holding one file in memory, with one account: alice.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "alice:secret" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "solution":
        await Fetch("bob before add_account", "--digest", "-u", "bob:builder");
        await AddAccount("bob", "builder");
        await Fetch("bob after add_account", "--digest", "-u", "bob:builder");
        await RemoveAccount(server, "bob");
        await Fetch("bob after remove_account", "--digest", "-u", "bob:builder");
        break;
    case "wrong-password":
        await AddAccount("bob", "builder");
        await Fetch("bob, wrong password", "--digest", "-u", "bob:wrong");
        break;
    case "no-overwrite":
        await AddAccount("bob", "builder");
        await AddAccount("bob", "other");
        await Fetch("bob, first password", "--digest", "-u", "bob:builder");
        break;
    case "change-password":
        await AddAccount("bob", "builder");
        await RemoveAccount(server, "bob");
        await AddAccount("bob", "other");
        await Fetch("bob, new password", "--digest", "-u", "bob:other");
        break;
    case "no-colon":
        await AddAccount("carol:x", "pw");
        break;
    case "basic-in-clear":
        await AddAccount("bob", "builder");
        await Fetch("bob, Basic", "-u", "bob:builder");
        break;
    case "no-such-account":
        await AddAccount("bob", "builder");
        await RemoveAccount(server, "bob");
        await RemoveAccount(server, "bob");
        await RemoveAccount("s99", "alice");
        break;
    case "last-account":
        await RemoveAccount(server, "alice");
        await Fetch("no login");
        break;
    case "first-account":
        await RemoveAccount(server, "alice");
        await AddAccount("carol", "pw");
        await Fetch("no login");
        await Fetch("carol", "--digest", "-u", "carol:pw");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// add_account and remove_account answer the user back, or are a tool error that says why not.
async Task AddAccount(string user, string password) => Report("add_account " + user,
    await surl.CallToolAsync("add_account", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["user"] = user, ["password"] = password,
    }));

async Task RemoveAccount(string id, string user) => Report("remove_account " + user,
    await surl.CallToolAsync("remove_account", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["user"] = user,
    }));

void Report(string call, CallToolResult result) => Console.WriteLine(result.IsError == true
    ? $"{call}: error: {result.Content.OfType<TextContentBlock>().Single().Text}"
    : $"{call}: {result.StructuredContent!.Value.GetProperty("user").GetString()}");

// The curl tool runs curl's own arguments; -w "%{http_code}" puts the status after the body.
async Task Fetch(string who, params string[] login)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-w", "%{http_code}" }.Concat(login).Append(url + "hello.txt").ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
    int exitCode = answer.RootElement.GetProperty("exitCode").GetInt32();
    Console.WriteLine($"{who}: {output[^3..]} {output[..^3].TrimEnd('\n')}".TrimEnd() + $" (curl exit {exitCode})");
}
