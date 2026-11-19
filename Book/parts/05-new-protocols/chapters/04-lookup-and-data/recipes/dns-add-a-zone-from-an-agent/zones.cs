// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent adds a DNS zone to a running surl through surl-mcp, reads it back with read_zone, and
// looks a name up with curl through curl-mcp. Run: dotnet run zones.cs -- <case>
//   solution            add_dns_zone, read_zone, then curl looks up www.example.com
//   zone-as-served      read_zone with an apex: the zone's records, names in full
//   refused-until-added curl looks up www.example.com before and after add_dns_zone
//   no-such-name        curl looks up a name the zone does not hold
//   second-zone         add_dns_zone again for the same apex
//   no-soa              add_dns_zone with a zone that has no SOA record
//   no-such-zone        read_zone for an apex nobody added
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using System.Text.RegularExpressions;
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

// A DNS server on a free port, holding no zone yet.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "dns://127.0.0.1:0" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string dns = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// A master file; names without a final dot are relative to the apex.
const string Zone =
    "$TTL 3600\n" +
    "@    IN SOA ns1 admin 1 7200 3600 1209600 300\n" +
    "@    IN NS  ns1\n" +
    "ns1  IN A   127.0.0.1\n" +
    "www  IN A   192.0.2.10\n";

switch (which)
{
    case "solution":
        await AddZone("example.com", Zone);
        await ReadZone(null);
        await Lookup("www.example.com");
        break;
    case "zone-as-served":
        await AddZone("example.com", Zone);
        await ReadZone("example.com");
        break;
    case "refused-until-added":
        await Lookup("www.example.com");
        await AddZone("example.com", Zone);
        await Lookup("www.example.com");
        break;
    case "no-such-name":
        await AddZone("example.com", Zone);
        await Lookup("nope.example.com");
        break;
    case "second-zone":
        await AddZone("example.com", Zone);
        await AddZone("example.com", Zone.Replace("192.0.2.10", "192.0.2.99"));
        await Lookup("www.example.com");
        break;
    case "no-soa":
        await AddZone("example.org", "$TTL 3600\nwww IN A 192.0.2.1\n");
        await ReadZone(null);
        break;
    case "no-such-zone":
        await AddZone("example.com", Zone);
        await ReadZone("example.net");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// add_dns_zone parses the zone and serves it at once, as if it had been a file at start.
async Task AddZone(string apex, string text)
{
    CallToolResult result = await surl.CallToolAsync("add_dns_zone", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["apex"] = apex, ["zone_text"] = text,
    });
    Console.WriteLine(result.IsError == true
        ? $"add_dns_zone {apex}: error: {result.Content.OfType<TextContentBlock>().Single().Text}"
        : $"add_dns_zone {apex}: {result.StructuredContent!.Value.GetProperty("byte_count")} bytes");
}

// read_zone with no apex lists the apexes served; with one, it answers that zone's records.
async Task ReadZone(string? apex)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = server };
    if (apex is not null)
    {
        arguments["apex"] = apex;
    }

    CallToolResult result = await surl.CallToolAsync("read_zone", arguments);
    if (result.IsError == true)
    {
        Console.WriteLine($"read_zone {apex}: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
    }
    else if (apex is null)
    {
        Console.WriteLine($"read_zone: {result.StructuredContent!.Value.GetProperty("zones").GetRawText()}");
    }
    else
    {
        Console.Write($"read_zone {apex}:\n{result.StructuredContent!.Value.GetProperty("zone").GetString()}");
    }
}

// The curl tool asks over the dns: URL; the program prints the status and the answer's records.
async Task Lookup(string name)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", dns + name },
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string status = Regex.Match(output, @"status: (\w+)").Groups[1].Value;
    Console.WriteLine($"curl exit {answer.RootElement.GetProperty("exitCode")}, {name}: {status}");
    Match records = Regex.Match(output, @";; ANSWER SECTION:\n(.*?)\n(?:\n|\z)", RegexOptions.Singleline);
    foreach (string line in records.Groups[1].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        Console.WriteLine("  " + line);
    }
}
