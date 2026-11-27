// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a surl-mcp server with two accounts, locks one of them out with a fault rule,
// logs in as each through curl-mcp, and lifts the rule again.
// Run: dotnet run lock-out.cs [-- lift|wrong|calm|once|rule|events]
//   lift    alice, bob locked out, the rule removed, bob again (the default)
//   wrong   bob's right and wrong password while locked out
//   calm    bob with no rule at all
//   once    a rule that applies one time
//   rule    what list_faults answers
//   events  login_judged and fault_applied events
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "lift";

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

// Two accounts and one file, in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "alice:a1-secret", "--user", "bob:b2-secret" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

// The rule goes on after the server is up, so the agent decides when the lock-out starts.
string rule = "refuse-login;account=bob" + (which == "once" ? ";times=1" : "");
string? faultId = null;
if (which != "calm")
{
    CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = server, ["fault"] = rule });
    faultId = added.StructuredContent!.Value.GetProperty("fault_id").GetString();
}

switch (which)
{
    case "lift":
        Console.WriteLine($"alice: {await Status("alice:a1-secret")}");
        Console.WriteLine($"bob: {await Status("bob:b2-secret")}");
        CallToolResult removed = await surl.CallToolAsync("remove_fault", new Dictionary<string, object?> { ["server_id"] = server, ["fault_id"] = faultId });
        Console.WriteLine($"removed {faultId}, tool error: {(removed.IsError == true ? "yes" : "no")}");
        Console.WriteLine($"bob: {await Status("bob:b2-secret")}");
        break;
    case "wrong":
        Console.WriteLine($"bob, right password: {await Status("bob:b2-secret")}");
        Console.WriteLine($"bob, wrong password: {await Status("bob:not-his-password")}");
        break;
    case "calm":
        Console.WriteLine($"bob: {await Status("bob:b2-secret")}");
        break;
    case "once":
        Console.WriteLine($"bob: {await Status("bob:b2-secret")}");
        Console.WriteLine($"bob: {await Status("bob:b2-secret")}");
        Console.WriteLine($"uses left: {await Faults()}");
        break;
    case "rule":
        Console.WriteLine(await Faults());
        break;
    case "events":
        await Status("alice:a1-secret");
        await Status("bob:b2-secret");
        foreach (JsonElement e in (await Events("login_judged")).EnumerateArray())
            Console.WriteLine($"{e.GetProperty("mechanism").GetString()} {e.GetProperty("account").GetString()}: {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
        foreach (JsonElement e in (await Events("fault_applied")).EnumerateArray())
            Console.WriteLine($"rule {e.GetProperty("rule_id").GetString()} applied: {e.GetProperty("fault_kind").GetString()}");
        break;
}
return 0;

// curl-mcp's curl tool: a Digest login as user:password; -w prints the status code after the body.
async Task<string> Status(string login)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "--digest", "-u", login, "-o", "hello.out", "-w", "%{http_code}", url },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
}

// surl-mcp's list_faults: the rule's ID, its text, and how many uses it has left.
async Task<string> Faults()
{
    CallToolResult listed = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server });
    JsonElement only = listed.StructuredContent!.Value.GetProperty("faults").EnumerateArray().Single();
    JsonElement left = only.GetProperty("remaining");
    string remaining = left.ValueKind == JsonValueKind.Null ? "no limit" : left.ToString();
    return which == "once" ? remaining : $"{only.GetProperty("fault_id").GetString()}: {only.GetProperty("item").GetString()}, remaining {remaining}";
}

// surl-mcp's get_events of one kind, oldest first.
async Task<JsonElement> Events(string kind)
{
    CallToolResult result = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = server, ["kinds"] = new[] { kind } });
    return result.StructuredContent!.Value.GetProperty("events").Clone();
}
