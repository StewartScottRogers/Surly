// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a provisioning job: it starts an LDAP server through surl-mcp, writes bob's entry
// and account as the job would, finds the entry by search and binds as it through curl-mcp, then
// waits for what the server recorded.
// Run: dotnet run provision-and-bind.cs [-- <mode>]
//   (no argument)  the job adds the entry and the account; bob binds with his password
//   guess          the same, but bob binds with a wrong password
//   entry-only     the job adds the entry, userPassword included, but no account
//   strict         a server that keeps the default and refuses a password sent in clear
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "";

const string Tree = """
    dn: dc=example,dc=com
    objectClass: domain
    dc: example

    dn: ou=people,dc=example,dc=com
    objectClass: organizationalUnit
    ou: people

    dn: uid=alice,ou=people,dc=example,dc=com
    objectClass: inetOrgPerson
    uid: alice
    cn: Alice Liddell
    sn: Liddell
    mail: alice@example.com
    """;

// What the job writes: bob's entry, and his account with the same password.
const string BobDn = "uid=bob,ou=people,dc=example,dc=com";
const string Bob = """
    dn: uid=bob,ou=people,dc=example,dc=com
    objectClass: inetOrgPerson
    uid: bob
    cn: Bob Jones
    sn: Jones
    mail: bob@example.com
    userPassword: b0b-pass
    """;

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

// The server: tester is the account the test searches as. A password over ldap:// needs
// --allow-plaintext-auth, which strict leaves out.
var arguments = new List<string> { "--user", "tester:t3st-pass" };
if (mode != "strict")
{
    arguments.Add("--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ldap://127.0.0.1:0" }, ["arguments"] = arguments.ToArray(),
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
await Tool("add_ldap_entries", new() { ["ldif"] = Tree });

if (mode == "strict")
{
    Console.WriteLine($"bind as tester: {await Search("ou=people,dc=example,dc=com?cn?sub?(uid=alice)", "tester:t3st-pass")}");
    return 0;
}

const string FindBob = "ou=people,dc=example,dc=com?cn,mail?sub?(uid=bob)";
Console.WriteLine($"before the job: {await Search(FindBob, "tester:t3st-pass")}");

// The job. The agent stands in for it here; yours runs its own code against the server.
await Tool("add_ldap_entries", new() { ["ldif"] = Bob });
if (mode != "entry-only")
{
    await Tool("add_account", new() { ["user"] = "bob", ["password"] = "b0b-pass" });
}

Console.WriteLine($"search as tester: {await Search(FindBob, "tester:t3st-pass")}");
string password = mode == "guess" ? "guess" : "b0b-pass";
Console.WriteLine($"bind as bob: {await Search($"{BobDn}?uid?base", $"{BobDn}:{password}")}");

// What the server recorded, each read waiting up to 10 seconds for its event.
JsonElement login = (await Tool("wait_for_event", new()
{
    ["kinds"] = new[] { "login_judged" }, ["account"] = "bob", ["timeout_ms"] = 10000,
})).GetProperty("event");
Console.WriteLine($"recorded: {login.GetProperty("scheme")} {login.GetProperty("mechanism")} {login.GetProperty("account")} {(login.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
JsonElement seeded = (await Tool("wait_for_event", new()
{
    ["kinds"] = new[] { "store_changed" }, ["store"] = "directory", ["name"] = BobDn, ["timeout_ms"] = 10000,
})).GetProperty("event");
Console.WriteLine($"recorded: {seeded.GetProperty("store")} {seeded.GetProperty("change")} {seeded.GetProperty("name")}");

CallToolResult read = await surl.CallToolAsync("read_directory", new Dictionary<string, object?> { ["server_id"] = id });
string exported = read.StructuredContent!.Value.GetProperty("ldif").GetString()!;
Console.WriteLine($"export: bob {Has(exported, $"dn: {BobDn}")}, userPassword {Has(exported, "userPassword")}, b0b-pass {Has(exported, "b0b-pass")}");
return 0;

static string Has(string text, string what) => text.Contains(what) ? "present" : "absent";

// A surl-mcp tool on this server; its structured answer.
async Task<JsonElement> Tool(string name, Dictionary<string, object?> more)
{
    more["server_id"] = id;
    CallToolResult result = await surl.CallToolAsync(name, more);
    if (result.IsError == true)
    {
        throw new InvalidOperationException($"{name}: {result.Content.OfType<TextContentBlock>().First().Text}");
    }

    return result.StructuredContent!.Value;
}

// One search through curl-mcp's curl tool, bound as user:password. The entries print as one line,
// each DN with its attributes, so the layout of the LDAP library underneath does not matter; a
// refusal prints curl's exit code.
async Task<string> Search(string search, string login)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", login, url + search },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int exitCode = answer.RootElement.GetProperty("exitCode").GetInt32();
    if (exitCode != 0)
    {
        return $"refused, curl exit {exitCode}";
    }

    JsonElement output = answer.RootElement.GetProperty("standardOutput");
    string text = output.ValueKind == JsonValueKind.Object ? output.GetProperty("content").GetString() ?? "" : "";
    string[] entries = text.Replace("\r", "").Split("DN: ", StringSplitOptions.RemoveEmptyEntries)
        .Select(e => string.Join(", ", e.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())))
        .Order().ToArray();
    return $"{entries.Length} [{string.Join(" | ", entries)}]";
}
