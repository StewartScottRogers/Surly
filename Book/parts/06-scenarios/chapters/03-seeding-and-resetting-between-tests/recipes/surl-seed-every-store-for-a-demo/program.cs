// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fills every store of one surl-mcp server for a demo: a web page, a mail, a retained MQTT
// message, a Redis key, directory entries and a DNS zone, then reads each back.
// Run: dotnet run program.cs -- <case>
//   seed       seed all six stores and read each back with surl-mcp (the default)
//   clients    seed, then fetch the page, the zone and the mail with curl-mcp, as the demo's clients would
//   no-login   seed, then ask Redis, LDAP and MQTT with no login
//   refused    two seeds the server refuses, and what they left behind
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "seed";

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

// One server, seven listeners on free ports, one mailbox.
JsonElement started = (await Surl("start_server", new()
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "mqtt://127.0.0.1:0", "redis://127.0.0.1:0", "ldap://127.0.0.1:0", "dns://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "ann@example.com:s3cret" },
})).StructuredContent!.Value;
string id = started.GetProperty("server_id").GetString()!;
string[] url = [.. started.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!)];

const string People = "dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n\n"
    + "dn: uid=ann,dc=example,dc=com\nobjectClass: inetOrgPerson\nuid: ann\ncn: Ann Lee\nsn: Lee\nmail: ann@example.com\n";
const string Zone = "$ORIGIN shop.test.\n$TTL 300\n@ IN SOA ns1 admin 1 3600 600 86400 300\n@ IN NS ns1\nns1 IN A 127.0.0.1\nwww IN A 127.0.0.1\n";

if (which == "refused")
{
    // A mail for an address with no mailbox, and a batch of two entries whose second has no parent.
    CallToolResult mail = await Surl("seed_mail", new() { ["server_id"] = id, ["to"] = "bob@example.com", ["message"] = "Subject: hi\r\n\r\nhello\r\n" });
    Console.WriteLine($"seed_mail bob: tool error {Yes(mail)}, {Text(mail)}");
    await Surl("add_ldap_entries", new() { ["server_id"] = id, ["ldif"] = People });
    CallToolResult batch = await Surl("add_ldap_entries", new()
    {
        ["server_id"] = id,
        ["ldif"] = "dn: uid=cy,dc=example,dc=com\nobjectClass: inetOrgPerson\nuid: cy\ncn: Cy\nsn: Cy\n\n"
            + "dn: uid=dee,ou=gone,dc=example,dc=com\nobjectClass: inetOrgPerson\nuid: dee\ncn: Dee\nsn: Dee\n",
    });
    Console.WriteLine($"add_ldap_entries cy, dee: tool error {Yes(batch)}, {Text(batch)}");
    string ldif = (await Surl("read_directory", new() { ["server_id"] = id })).StructuredContent!.Value.GetProperty("ldif").GetString()!;
    Console.WriteLine($"directory holds uid=cy: {(ldif.Contains("uid=cy,") ? "yes" : "no")}");
    return 0;
}

// The seeds, one tool per store. None needs a login or a client.
Console.WriteLine($"put_file: {Text(await Surl("put_file", new() { ["server_id"] = id, ["path"] = "/index.html", ["text"] = "<h1>Demo shop</h1>\n" }))}");
Console.WriteLine($"seed_mail: {Text(await Surl("seed_mail", new() { ["server_id"] = id, ["to"] = "ann@example.com", ["message"] = "From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Your order 42\r\n\r\nShipped today.\r\n" }))}");
Console.WriteLine($"publish_message: {Text(await Surl("publish_message", new() { ["server_id"] = id, ["topic"] = "shop/status", ["payload"] = "open", ["retain"] = true }))}");
Console.WriteLine($"set_key: {Text(await Surl("set_key", new() { ["server_id"] = id, ["key"] = "cart:ann", ["value"] = "2 items" }))}");
Console.WriteLine($"add_ldap_entries: {Text(await Surl("add_ldap_entries", new() { ["server_id"] = id, ["ldif"] = People }))}");
Console.WriteLine($"add_dns_zone: {Text(await Surl("add_dns_zone", new() { ["server_id"] = id, ["apex"] = "shop.test", ["zone_text"] = Zone }))}");

switch (which)
{
    case "clients":
        // What the demo's clients see over the wire: the page, a name in the zone, the mail.
        Console.WriteLine($"http: {Out(await Curl("-s", url[0] + "index.html"))}");
        Console.WriteLine($"dns: {Out(await Curl("-s", url[6] + "www.shop.test")).Split('\n').First(l => l.StartsWith("www.shop.test."))}");
        Console.WriteLine($"imap: {Out(await Curl("-s", "-u", "ann@example.com:s3cret", url[2] + "INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)"))}");
        break;

    case "no-login":
        // The other three need an account: the server is surly to a client that brings none.
        Console.WriteLine($"redis: exitCode {Exit(await Curl("-s", url[4] + "GET/cart:ann"))}");
        Console.WriteLine($"ldap: exitCode {Exit(await Curl("-s", "-u", ":", url[5] + "dc=example,dc=com??sub?(uid=ann)"))}");
        Console.WriteLine($"mqtt: exitCode {Exit(await Curl("-s", "--max-time", "5", url[3] + "shop/status"))}");
        break;

    default:
        // Read each store back without a client.
        JsonElement file = (await Surl("read_file", new() { ["server_id"] = id, ["path"] = "/index.html" })).StructuredContent!.Value;
        Console.WriteLine($"read_file: {file.GetProperty("content_type")} {file.GetProperty("text").GetString()!.TrimEnd()}");
        JsonElement inbox = (await Surl("list_messages", new() { ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX" })).StructuredContent!.Value;
        Console.WriteLine($"list_messages: {string.Join(", ", inbox.GetProperty("messages").EnumerateArray().Select(m => $"uid {m.GetProperty("uid")}, {m.GetProperty("size")} bytes"))}");
        JsonElement retained = (await Surl("read_retained_message", new() { ["server_id"] = id, ["topic"] = "shop/status" })).StructuredContent!.Value;
        Console.WriteLine($"read_retained_message: {retained.GetProperty("topic")} {retained.GetProperty("text")}");
        JsonElement key = (await Surl("read_key", new() { ["server_id"] = id, ["key"] = "cart:ann" })).StructuredContent!.Value;
        Console.WriteLine($"read_key: {key.GetProperty("key")} {key.GetProperty("type")} {key.GetProperty("text")}");
        string ldif = (await Surl("read_directory", new() { ["server_id"] = id })).StructuredContent!.Value.GetProperty("ldif").GetString()!;
        Console.WriteLine($"read_directory: {string.Join(", ", ldif.Split('\n').Where(l => l.StartsWith("dn: ")).Select(l => l[4..]))}");
        string zone = (await Surl("read_zone", new() { ["server_id"] = id, ["apex"] = "shop.test" })).StructuredContent!.Value.GetProperty("zone").GetString()!;
        Console.WriteLine($"read_zone: {zone.Split('\n').First(l => l.StartsWith("www."))}");
        break;
}

return 0;

Task<CallToolResult> Surl(string tool, Dictionary<string, object?> arguments) => surl.CallToolAsync(tool, arguments).AsTask();
string Text(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;
string Yes(CallToolResult result) => result.IsError == true ? "yes" : "no";

// curl-mcp answers in text, as JSON: exitCode and the standard output.
async Task<JsonElement> Curl(params string[] argv) =>
    JsonDocument.Parse(Text(await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv }))).RootElement;
string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "").Trim().Replace("\t", " ");
string Exit(JsonElement answer) => answer.GetProperty("exitCode").GetRawText();
