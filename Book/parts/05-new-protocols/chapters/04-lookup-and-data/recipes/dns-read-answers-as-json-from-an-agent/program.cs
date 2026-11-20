// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds a DNS zone into surl-mcp and reads MX, TXT and AAAA records as JSON through curl-mcp,
// over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   lookup            seed the zone, look up MX, TXT and AAAA (the default)
//   outside-the-zone  look up a name no zone holds
//   no-such-type      look up a type the name has no record of
//   no-zone           look up in a server that was given no zone
//   tcp               look up over TCP instead of UDP
//   read-zone         list the zones the server holds and read one back
//   second-zone       seed a second zone for the same apex
//   no-dns-listener   seed a zone into a server with only an HTTP listener
//   transfer          look up with curl-mcp's transfer tool, which has no JSON switch
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "lookup";

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

// A DNS server on a free port; UDP and TCP share it. In no-dns-listener it is an HTTP server instead.
string listen = which == "no-dns-listener" ? "http://127.0.0.1:0" : "dns://127.0.0.1:0";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { listen },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string dns = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

const string zone = """
    $ORIGIN example.test.
    $TTL 300
    @    IN SOA  ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300
    @    IN NS   ns1.example.test.
    @    IN MX   10 mail.example.test.
    @    IN MX   20 mail2.example.test.
    @    IN TXT  "v=spf1 mx -all" "second string"
    @    IN AAAA 2001:db8::25
    mail IN A    192.0.2.25
    """;

// add_dns_zone adds a zone, from zone-file text, to the running server.
CallToolResult last = new();
if (which != "no-zone")
{
    last = await surl.CallToolAsync("add_dns_zone", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["apex"] = "example.test", ["zone_text"] = zone,
    });
    if (last.IsError != true)
    {
        Console.WriteLine($"add_dns_zone: {last.StructuredContent!.Value.GetProperty("apex")}");
    }
}

switch (which)
{
    case "lookup":
        await Lookup("example.test", "MX");
        await Lookup("example.test", "TXT");
        await Lookup("example.test", "AAAA");
        break;
    case "outside-the-zone":
        await Lookup("nope.invalid", "A");
        break;
    case "no-such-type":
        await Lookup("example.test", "SRV");
        break;
    case "no-zone":
        await Lookup("example.test", "MX");
        break;
    case "tcp":
        await Lookup("example.test", "AAAA", "--dns-transport", "tcp");
        break;
    case "read-zone":
        // read_zone with no apex lists the zones; with one it answers the zone as master-file text.
        last = await surl.CallToolAsync("read_zone", new Dictionary<string, object?> { ["server_id"] = server });
        Console.WriteLine($"read_zone: {last.StructuredContent!.Value.GetProperty("zones")[0]}");
        last = await surl.CallToolAsync("read_zone", new Dictionary<string, object?> { ["server_id"] = server, ["apex"] = "example.test" });
        Console.Write(last.StructuredContent!.Value.GetProperty("zone").GetString()!.Split('\n')[2] + "\n");
        last = await surl.CallToolAsync("read_zone", new Dictionary<string, object?> { ["server_id"] = server, ["apex"] = "nope.test" });
        Console.WriteLine($"read_zone nope.test: error: {Text(last)}");
        break;
    case "second-zone":
        last = await surl.CallToolAsync("add_dns_zone", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["apex"] = "example.test", ["zone_text"] = zone,
        });
        Console.WriteLine($"add_dns_zone again: error: {Text(last)}");
        break;
    case "no-dns-listener":
        Console.WriteLine($"add_dns_zone: error: {Text(last)}");
        break;
    case "transfer":
        // transfer takes a URL and no switches, so a dns URL comes back as dig-like text.
        last = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = dns + "example.test?type=MX" });
        JsonElement sent = JsonDocument.Parse(Text(last)).RootElement;
        string body = sent.GetProperty("body").GetProperty("content").GetString()!;
        Console.WriteLine($"transfer: statusCode {sent.GetProperty("statusCode")}, body starts {body.Split('\n')[0].Split(',')[1].Trim()}");
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

// The exit code says whether the last call was a tool error.
Console.WriteLine($"tool error: {(last.IsError == true ? "yes" : "no")}");
return last.IsError == true ? 1 : 0;

// The curl tool runs curl's own arguments: --dns-json asks for the answer as one line of JSON.
async Task Lookup(string name, string type, params string[] more)
{
    last = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "--dns-json" }.Concat(more).Append($"{dns}{name}?type={type}").ToArray(),
    });
    JsonElement result = JsonDocument.Parse(Text(last)).RootElement;
    JsonElement answer = JsonDocument.Parse(result.GetProperty("standardOutput").GetProperty("content").GetString()!).RootElement;
    JsonElement records = answer.GetProperty("answer");
    Console.WriteLine($"{type}: exit {result.GetProperty("exitCode")}, {answer.GetProperty("status")}, {records.GetArrayLength()} records");
    foreach (JsonElement record in records.EnumerateArray())
    {
        Console.WriteLine($"  {record.GetProperty("data")}");
    }
}

static string Text(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;
