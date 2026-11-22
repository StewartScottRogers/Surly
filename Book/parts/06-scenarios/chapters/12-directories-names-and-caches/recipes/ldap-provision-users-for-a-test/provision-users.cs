// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent provisions a set of users for a test: it starts an LDAP server through surl-mcp, adds the
// users in one batch, reads the directory back as LDIF, and searches for each user through curl-mcp.
// Run: dotnet run provision-users.cs [-- <mode>]
//   (no argument)  the set is added, read back, searched for, then the server is reset
//   bad-batch      the set holds one user whose parent does not exist
//   strict         a server without --allow-anonymous, searched with no name and password
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "";

// The users the test needs: uid, full name and the branch they sit under.
var users = new List<(string Uid, string Name, string Branch)>
{
    ("ann", "Ann Archer", "ou=people"), ("ben", "Ben Baker", "ou=people"), ("cat", "Cat Carter", "ou=people"),
};
if (mode == "bad-batch")
{
    users.Add(("dan", "Dan Dyer", "ou=contractors"));
}

// One LDIF batch: the top of the tree, then one entry per user.
var ldif = new StringBuilder("""
    dn: dc=example,dc=com
    objectClass: domain
    dc: example

    dn: ou=people,dc=example,dc=com
    objectClass: organizationalUnit
    ou: people

    """);
foreach (var (uid, name, branch) in users)
{
    ldif.Append($"\ndn: uid={uid},{branch},dc=example,dc=com\nobjectClass: inetOrgPerson\nuid: {uid}\ncn: {name}\nsn: {name.Split(' ')[1]}\nuserPassword: {uid}-pass\n");
}

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

// The server: an empty directory that anyone may search, except in strict mode.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ldap://127.0.0.1:0" },
    ["arguments"] = mode == "strict" ? Array.Empty<string>() : new[] { "--allow-anonymous" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

CallToolResult added = await surl.CallToolAsync("add_ldap_entries", new Dictionary<string, object?>
{
    ["server_id"] = id, ["ldif"] = ldif.ToString(),
});
Console.WriteLine(added.IsError == true ? "added: refused" : $"added: entry_count {added.StructuredContent!.Value.GetProperty("entry_count")}");

if (added.IsError != true)
{
    // Wait, at most 10 seconds, for the batch to be recorded, then read the directory back.
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "directory",
        ["name"] = $"uid={users[^1].Uid},ou=people,dc=example,dc=com", ["timeout_ms"] = 10000,
    });
    Console.WriteLine($"recorded: {(waited.StructuredContent!.Value.TryGetProperty("event", out JsonElement e) ? e.GetProperty("change").GetString() : "nothing")}");
}

// read_directory answers {"ldif": ...}, the directory as LDIF text.
string exported = (await surl.CallToolAsync("read_directory", new Dictionary<string, object?> { ["server_id"] = id }))
    .StructuredContent!.Value.GetProperty("ldif").GetString()!;
string[] dns = exported.Replace("\r", "").Split('\n').Where(l => l.StartsWith("dn: ")).Select(l => l[4..]).ToArray();
Console.WriteLine($"read back: {dns.Length} [{string.Join(" | ", dns)}]");
Console.WriteLine($"read back: userPassword {(exported.Contains("userPassword") ? "present" : "absent")}");

foreach (var (uid, _, _) in users.Take(3))
{
    Console.WriteLine($"search {uid}: {await Search($"dc=example,dc=com?cn?sub?(uid={uid})")}");
}

if (mode == "")
{
    // Between tests: a reset returns the directory to how the server started, empty.
    await surl.CallToolAsync("reset_server", new Dictionary<string, object?> { ["server_id"] = id });
    Console.WriteLine($"after reset, search ann: {await Search("dc=example,dc=com?cn?sub?(uid=ann)")}");
}

return 0;

// One anonymous search (an empty name and password, curl's -u :) through curl-mcp's curl tool.
// The entries print as one line, so the layout of the LDAP library underneath does not matter;
// a failure prints curl's exit code.
async Task<string> Search(string search)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", ":", url + search },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int exitCode = answer.RootElement.GetProperty("exitCode").GetInt32();
    if (exitCode != 0)
    {
        return $"curl exit {exitCode}";
    }

    JsonElement output = answer.RootElement.GetProperty("standardOutput");
    string text = output.ValueKind == JsonValueKind.Object ? output.GetProperty("content").GetString() ?? "" : "";
    string[] entries = text.Replace("\r", "").Split("DN: ", StringSplitOptions.RemoveEmptyEntries)
        .Select(e => string.Join(", ", e.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())))
        .Order().ToArray();
    return $"{entries.Length} [{string.Join(" | ", entries)}]";
}
