// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an LDAP directory and a web server that checks logins against it through surl-mcp, seeds
// the directory with the people, and logs in to the web server as one of them with curl-mcp's curl tool.
// server.pem and server.key are in the folder.
// Run: dotnet run program.cs -- <case>
//   login     alice and bob log in with their directory passwords (the default)
//   refused   a wrong password, a name not in the directory, and no login
//   events    what each server recorded about alice's login
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "login";

// The service account's password, in a file so it is never in a command line.
string here = Directory.GetCurrentDirectory();
File.WriteAllText("svc.pass", "svcpass\n");

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
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

// The directory: LDAP with StartTLS, and an account for each person and for svc, the web server's service account.
(string directory, string ldapUrl) = await Start(["ldap://127.0.0.1:0"],
    ["--cert", "server.pem", "--key", "server.key", "-u", "alice:wonderland", "-u", "bob:builder", "-u", "svc:svcpass"], null);
CallToolResult seeded = await surl.CallToolAsync("add_ldap_entries", new Dictionary<string, object?>
{
    ["server_id"] = directory,
    ["ldif"] = """
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
        """,
});
Console.WriteLine($"seeded: {seeded.StructuredContent!.Value.GetProperty("entry_count")} entries");

// The web server: the report over HTTPS, ops its one account of its own, every other login checked
// against the directory under ou=people, searched as svc.
int port = new Uri(ldapUrl).Port;
(string web, string webUrl) = await Start(["https://127.0.0.1:0"],
    ["--cert", "server.pem", "--key", "server.key", "-u", "ops:opspass", "--user-ldap",
     $"ldap://127.0.0.1:{port}/ou=people,dc=example,dc=com;bind-dn=cn=svc,dc=example,dc=com;bind-password-file=svc.pass;ca-file=server.pem"],
    new Dictionary<string, string> { ["report.txt"] = "q3 total: 1200\n" });

switch (which)
{
    case "refused":
        Console.WriteLine($"alice, wrong password: {await Get("alice:queenofhearts")}");
        Console.WriteLine($"carol: {await Get("carol:x")}");
        Console.WriteLine($"no login: {await Get(null)}");
        break;
    case "events":
        Console.WriteLine($"alice: {await Get("alice:wonderland")}");
        await Judged("web", web);
        await Judged("directory", directory);
        break;
    default:
        Console.WriteLine($"alice: {await Get("alice:wonderland")}");
        Console.WriteLine($"bob: {await Get("bob:builder")}");
        break;
}

return 0;

// start_server: answers the server_id and the first bound URL.
async Task<(string Id, string Url)> Start(string[] listen, string[] arguments, Dictionary<string, string>? files)
{
    var call = new Dictionary<string, object?> { ["listen_urls"] = listen, ["arguments"] = arguments };
    if (files is not null) call["files"] = files;
    CallToolResult started = await surl.CallToolAsync("start_server", call);
    JsonElement s = started.StructuredContent!.Value;
    return (s.GetProperty("server_id").GetString()!, s.GetProperty("bound_urls")[0].GetString()!);
}

// One curl tool call for the report, with HTTP Basic (-u) when a login is given: the status, then the body.
async Task<string> Get(string? login)
{
    var words = new List<string> { "-s", "-S", "--max-time", "30", "--cacert", "server.pem", "-w", " %{http_code}" };
    if (login is not null) words.AddRange(["-u", login]);
    words.Add($"{webUrl}report.txt");
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words.ToArray() });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string printed = run.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string status = printed[(printed.LastIndexOf(' ') + 1)..];
    return status == "200" ? $"200 {printed[..printed.LastIndexOf(' ')].TrimEnd()}" : status;
}

// get_events login_judged: each login the server judged, in order.
async Task Judged(string label, string id)
{
    CallToolResult got = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "login_judged" } });
    foreach (JsonElement e in got.StructuredContent!.Value.GetProperty("events").EnumerateArray())
        Console.WriteLine($"{label}: {e.GetProperty("scheme")} {e.GetProperty("account")} {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}
