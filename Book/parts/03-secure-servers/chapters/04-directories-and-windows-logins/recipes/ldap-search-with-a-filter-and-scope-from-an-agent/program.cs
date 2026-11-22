// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds a directory with surl-mcp and searches it by base, scope, filter and attributes with
// curl-mcp, over the official MCP SDK. Run: dotnet run program.cs
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// The directory: two branches under dc=example,dc=com, as LDIF text.
const string Ldif = """
    dn: dc=example,dc=com
    objectClass: domain
    dc: example

    dn: ou=people,dc=example,dc=com
    objectClass: organizationalUnit
    ou: people

    dn: cn=alice,ou=people,dc=example,dc=com
    objectClass: person
    cn: alice
    sn: Liddell
    mail: alice@example.com

    dn: cn=bob,ou=people,dc=example,dc=com
    objectClass: person
    cn: bob
    sn: Builder
    mail: bob@example.com

    dn: ou=groups,dc=example,dc=com
    objectClass: organizationalUnit
    ou: groups

    dn: cn=staff,ou=groups,dc=example,dc=com
    objectClass: groupOfNames
    cn: staff
    member: cn=alice,ou=people,dc=example,dc=com
    """;

// surl-mcp: an LDAP server that answers searches without a bind, and one that does not. Both start empty.
var urls = new Dictionary<string, string>();
string server = await Start("--allow-anonymous");
string strict = await Start();
CallToolResult added = await surl.CallToolAsync("add_ldap_entries", new Dictionary<string, object?>
{
    ["server_id"] = server, ["ldif"] = Ldif,
});
Console.WriteLine($"add_ldap_entries: {added.StructuredContent!.Value}");

await Search(server, "one, (sn=B*)", "ou=people,dc=example,dc=com?mail?one?(sn=B*)");
await Search(server, "base", "ou=people,dc=example,dc=com?ou?base?(objectClass=*)");
await Search(server, "one from the top", "dc=example,dc=com?ou?one?(objectClass=organizationalUnit)");
await Search(server, "sub, (cn=staff)", "dc=example,dc=com?cn?sub?(cn=staff)");
await Search(server, "one, (cn=staff)", "dc=example,dc=com?cn?one?(cn=staff)");
await Search(server, "and", "dc=example,dc=com?cn?sub?(&(objectClass=person)(mail=*example.com))");
await Search(server, "not", "ou=people,dc=example,dc=com?cn?one?(!(cn=bob))");
await Search(server, "no match", "ou=people,dc=example,dc=com?cn?one?(cn=zed)");
await Search(server, "missing base", "ou=nowhere,dc=example,dc=com?cn?one?(cn=alice)");
await Search(strict, "strict", "dc=example,dc=com?cn?sub?(cn=alice)");
return 0;

async Task<string> Start(params string[] options)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "ldap://127.0.0.1:0" },
        ["arguments"] = options,
    });
    JsonElement answer = started.StructuredContent!.Value;
    urls[answer.GetProperty("server_id").GetString()!] = answer.GetProperty("bound_urls")[0].GetString()!;
    return answer.GetProperty("server_id").GetString()!;
}

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. -u : is the anonymous bind.
// Each entry prints on one line, its DN and attributes, and the exit code stands for the error message,
// so neither the layout nor the wording of the LDAP library underneath matters.
async Task Search(string id, string label, string search)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", ":", urls[id] + search },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string text = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "");
    string[] entries = text.Split("DN: ", StringSplitOptions.RemoveEmptyEntries);
    Console.WriteLine($"{label}: {entries.Length} [{string.Join(" | ", entries.Select(e => string.Join(", ", e.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()))))}] exit {answer.RootElement.GetProperty("exitCode")}");
}
