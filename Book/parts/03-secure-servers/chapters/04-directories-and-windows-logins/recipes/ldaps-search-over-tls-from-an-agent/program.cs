// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a directory that takes a password bind over LDAPS and refuses it over plain LDAP with
// surl-mcp, and binds to both with curl-mcp, over the official MCP SDK. Run: dotnet run program.cs
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. server.pem and
// server.key, a certificate and key for 127.0.0.1, are in it.
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

// surl-mcp: one server, an ldaps and an ldap listener, one account, nothing loosened.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ldaps://127.0.0.1:0", "ldap://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "alice:s3cret" },
});
JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string ldaps = bound[0].GetString()!, ldap = bound[1].GetString()!;
const string Search = "dc=example,dc=com?mail?sub?(cn=bob)";

// The directory starts empty; add_ldap_entries puts two entries into it from LDIF text.
CallToolResult added = await surl.CallToolAsync("add_ldap_entries", new Dictionary<string, object?>
{
    ["server_id"] = started.StructuredContent!.Value.GetProperty("server_id").GetString(),
    ["ldif"] = """
        dn: dc=example,dc=com
        objectClass: domain
        dc: example

        dn: cn=bob,dc=example,dc=com
        objectClass: person
        cn: bob
        mail: bob@example.com
        """,
});
Console.WriteLine($"add_ldap_entries: {added.StructuredContent!.Value}");

await Bind("ldaps, right password", "-k", "-u", "alice:s3cret", ldaps + Search);
await Bind("ldaps, wrong password", "-k", "-u", "alice:guess", ldaps + Search);
await Bind("ldap, right password", "-u", "alice:s3cret", ldap + Search);
await Bind("ldap, wrong password", "-u", "alice:guess", ldap + Search);
await Bind("ldaps, certificate checked", "-u", "alice:s3cret", ldaps + Search);
return 0;

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. -k skips the check of the
// certificate. The program prints the exit code and which of two refusals the error message names; the rest
// of the wording is the LDAP library's.
async Task Bind(string label, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(more).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string text = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "");
    string lines = string.Join(", ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));
    string error = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!;
    string reason = new[] { "Confidentiality Required", "Invalid Credentials" }.FirstOrDefault(error.Contains) ?? "";
    Console.WriteLine($"{label}: [{lines}] exit {answer.RootElement.GetProperty("exitCode")} {reason}".TrimEnd());
}
