// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds the directory of a running surl-mcp server in steps, reads it back with read_directory,
// and searches it with curl-mcp, over the official MCP SDK. Run: dotnet run program.cs
#:package ModelContextProtocol.Core@2.4.0

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

// The first batch is the tree's top and alice; the second adds bob, whose entry holds a password.
const string First = """
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
    """;
const string Second = """
    dn: cn=bob,ou=people,dc=example,dc=com
    objectClass: person
    cn: bob
    sn: Builder
    userPassword: b0bpass
    """;

// A batch is all or none: carol is fine, dave's parent does not exist.
const string Mixed = """
    dn: cn=carol,ou=people,dc=example,dc=com
    objectClass: person
    cn: carol
    sn: Singer

    dn: cn=dave,ou=nowhere,dc=example,dc=com
    objectClass: person
    cn: dave
    sn: Digger
    """;

const string People = "dc=example,dc=com?cn?sub?(objectClass=person)";

// surl-mcp: an LDAP server that answers searches without a bind. Its directory starts empty.
var urls = new Dictionary<string, string>();
string server = await Start("ldap://127.0.0.1:0", "--allow-anonymous");
string url = urls[server];
Console.WriteLine($"empty: {await Search(url, People)}");

Console.WriteLine($"first batch: {await Seed(server, First)}");
Console.WriteLine($"after the first batch: {await Search(url, People)}");
Console.WriteLine($"second batch: {await Seed(server, Second)}");
Console.WriteLine($"after the second batch: {await Search(url, People)}");

Console.WriteLine($"mixed batch: {await Seed(server, Mixed)}");
Console.WriteLine($"carol after it: {await Search(url, "dc=example,dc=com?cn?sub?(cn=carol)")}");
Console.WriteLine($"alice again: {await Seed(server, First[First.IndexOf("dn: cn=alice")..])}");
Console.WriteLine($"not LDIF: {await Seed(server, "this is not LDIF")}");

// read_directory answers the directory as LDIF, without going over LDAP.
CallToolResult read = await surl.CallToolAsync("read_directory", new Dictionary<string, object?> { ["server_id"] = server });
string exported = read.Content.OfType<TextContentBlock>().Single().Text;
Console.WriteLine($"read_directory: alice {Has(exported, "cn=alice,ou=people")}, bob {Has(exported, "cn=bob,ou=people")}, userPassword {Has(exported, "userPassword")}, b0bpass {Has(exported, "b0bpass")}");

// A server with no LDAP listener has no directory to seed.
string web = await Start("http://127.0.0.1:0");
Console.WriteLine($"http server: {await Seed(web, First)}");

// A second LDAP server has a directory of its own, empty again.
string other = await Start("ldap://127.0.0.1:0", "--allow-anonymous");
Console.WriteLine($"another server: {await Search(urls[other], People)}");
return 0;

static string Has(string text, string what) => text.Contains(what) ? "present" : "absent";

// start_server: the answer has the server's id and the URL it bound.
async Task<string> Start(string listen, params string[] options)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { listen },
        ["arguments"] = options,
    });
    JsonElement answer = started.StructuredContent!.Value;
    string id = answer.GetProperty("server_id").GetString()!;
    urls[id] = answer.GetProperty("bound_urls")[0].GetString()!;
    return id;
}

// add_ldap_entries: the entry count it added, or what the tool error says.
async Task<string> Seed(string id, string ldif)
{
    CallToolResult result = await surl.CallToolAsync("add_ldap_entries", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["ldif"] = ldif,
    });
    return result.IsError == true ? "refused" : $"entry_count {result.StructuredContent!.Value.GetProperty("entry_count")}";
}

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. -u : is the anonymous
// bind. Each entry prints on one line, its DN and attributes, sorted, and the exit code stands for the
// error message, so neither the layout nor the wording of the LDAP library underneath matters.
async Task<string> Search(string baseUrl, string search)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", ":", baseUrl + search },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement output = answer.RootElement.GetProperty("standardOutput");
    string text = output.ValueKind == JsonValueKind.Object ? output.GetProperty("content").GetString()!.Replace("\r", "") : "";
    string[] entries = text.Split("DN: ", StringSplitOptions.RemoveEmptyEntries)
        .Select(e => string.Join(", ", e.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())))
        .Order().ToArray();
    return $"{entries.Length} [{string.Join(" | ", entries)}] exit {answer.RootElement.GetProperty("exitCode")}";
}
