// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a web server that holds no passwords for the people: surl-mcp starts a directory and
// a web server that checks every login it does not hold against it, curl-mcp's curl tool logs in,
// and the agent manages the people in the directory alone while both run.
// Run: dotnet run ldap-logins.cs -- <case>
//   (none)    alice, whom only the directory holds, logs in (the default)
//   starter   carol: not in the directory, then an entry only, then an entry and an account
//   leaver    alice logs in, her directory account is removed, she logs in again
//   events    the starter's logins as the web server judged them
//   shadow    bob, whom the web server also holds, and a request with no login
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "run";

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

// The directory: LDAP with StartTLS, accounts for alice, bob and svc, then the people as entries.
(string directory, string ldap) = await Start("ldap://127.0.0.1:0",
    ["--cert", "server.pem", "--key", "server.key", "-u", "alice:wonderland", "-u", "bob:builder", "-u", "svc:svcpass"], null);
await AddEntries("""
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

    dn: uid=bob,ou=people,dc=example,dc=com
    objectClass: inetOrgPerson
    uid: bob
    cn: Bob Builder
    sn: Builder
    """);

// The web server: the report over HTTPS, every login it holds no account for checked against the
// directory, searched as svc. Its own accounts: ops, so every request needs a login, and an old bob.
string port = new Uri(ldap).Port.ToString();
(string web, string https) = await Start("https://127.0.0.1:0",
    ["--cert", "server.pem", "--key", "server.key", "-u", "ops:opspass", "-u", "bob:localpass", "--user-ldap",
     $"ldap://127.0.0.1:{port}/ou=people,dc=example,dc=com;bind-dn=cn=svc,dc=example,dc=com;bind-password-file=svc.pass;ca-file=server.pem"],
    new Dictionary<string, string> { ["report.txt"] = "q3 total: 1200\n" });

switch (which)
{
    case "starter" or "events":
        string before = await Get("carol:marvel");
        await AddEntries("""
            dn: uid=carol,ou=people,dc=example,dc=com
            objectClass: inetOrgPerson
            uid: carol
            cn: Carol Danvers
            sn: Danvers
            """);
        string entryOnly = await Get("carol:marvel");
        await surl.CallToolAsync("add_account", new Dictionary<string, object?> { ["server_id"] = directory, ["user"] = "carol", ["password"] = "marvel" });
        string both = await Get("carol:marvel");
        if (which == "starter")
        {
            Console.WriteLine($"carol, not in the directory: {before}");
            Console.WriteLine($"carol, entry only: {entryOnly}");
            Console.WriteLine($"carol, entry and account: {both}");
            break;
        }

        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = web, ["kinds"] = new[] { "login_judged" }, ["account"] = "carol",
        });
        foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
        {
            Console.WriteLine($"web judged: {e.GetProperty("scheme").GetString()} {e.GetProperty("mechanism").GetString()} carol {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
        }

        break;
    case "leaver":
        Console.WriteLine($"alice: {await Get("alice:wonderland")}");
        await surl.CallToolAsync("remove_account", new Dictionary<string, object?> { ["server_id"] = directory, ["user"] = "alice" });
        Console.WriteLine($"alice, account removed from the directory: {await Get("alice:wonderland")}");
        break;
    case "shadow":
        Console.WriteLine($"bob, directory password: {await Get("bob:builder")}");
        Console.WriteLine($"bob, the web server's own password: {await Get("bob:localpass")}");
        Console.WriteLine($"no login: {await Get(null)}");
        break;
    default:
        Console.WriteLine($"alice: {await Get("alice:wonderland")}");
        break;
}

return 0;

// start_server: the server's ID and its bound URL.
async Task<(string Id, string Url)> Start(string listen, string[] arguments, Dictionary<string, string>? files)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { listen }, ["arguments"] = arguments, ["files"] = files ?? [],
    });
    JsonElement s = started.StructuredContent!.Value;
    return (s.GetProperty("server_id").GetString()!, s.GetProperty("bound_urls")[0].GetString()!);
}

// add_ldap_entries: LDIF into the running directory, all or none.
async Task AddEntries(string ldif) =>
    await surl.CallToolAsync("add_ldap_entries", new Dictionary<string, object?> { ["server_id"] = directory, ["ldif"] = ldif });

// The curl tool: one GET of the report, with -u when a login is given; the status, and the body when it is 200.
async Task<string> Get(string? login)
{
    string[] argv = ["-s", "-S", "--max-time", "30", "--cacert", "server.pem", "-o", "report.out", "-w", "%{http_code}", .. login is null ? Array.Empty<string>() : ["-u", login], https + "report.txt"];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
    using JsonDocument doc = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    string status = doc.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();
    return status == "200" ? $"200 {File.ReadAllText(Path.Combine(here, "report.out")).TrimEnd()}" : status;
}
