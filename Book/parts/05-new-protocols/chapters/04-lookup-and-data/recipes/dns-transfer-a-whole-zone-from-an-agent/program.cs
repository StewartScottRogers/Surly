// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent transfers a whole DNS zone: surl-mcp starts the server and seeds the zone, curl-mcp asks for AXFR,
// over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   transfer                allow transfers, ask over TCP, list the records (the default)
//   refused-by-default      no --allow-zone-transfer, over TCP
//   refused-by-default-udp  no --allow-zone-transfer, over UDP
//   refused-over-udp        allow transfers, ask over UDP
//   not-an-apex             allow transfers, ask over TCP for a name inside the zone
//   text-answer             allow transfers, read the dig-like text and not the JSON
//   no-incremental-type     ask for IXFR
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "transfer";
bool allowed = !which.StartsWith("refused-by-default");

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

// A DNS server on a free port; UDP and TCP share it. --allow-zone-transfer goes in arguments, in surl's own words.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "dns://127.0.0.1:0" },
    ["arguments"] = allowed ? new[] { "--allow-zone-transfer" } : Array.Empty<string>(),
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string dns = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

CallToolResult last = await surl.CallToolAsync("add_dns_zone", new Dictionary<string, object?>
{
    ["server_id"] = server,
    ["apex"] = "example.test",
    ["zone_text"] = """
        $ORIGIN example.test.
        $TTL 300
        @    IN SOA  ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300
        @    IN NS   ns1.example.test.
        @    IN MX   10 mail.example.test.
        mail IN A    192.0.2.25
        ns1  IN A    192.0.2.53
        """,
});

// The curl tool runs curl's own arguments: the type AXFR in the URL, the transport and the JSON switch before it.
string name = which == "not-an-apex" ? "mail.example.test" : "example.test";
string type = which == "no-incremental-type" ? "IXFR" : "AXFR";
string transport = which is "refused-by-default-udp" or "refused-over-udp" ? "udp" : "tcp";
var curlArgs = new List<string> { "-s", "-S", "--dns-transport", transport };
if (which != "text-answer")
{
    curlArgs.Add("--dns-json");
}

curlArgs.Add($"{dns}{name}?type={type}");
last = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs.ToArray() });
JsonElement result = JsonDocument.Parse(last.Content.OfType<TextContentBlock>().Single().Text).RootElement;
int exit = result.GetProperty("exitCode").GetInt32();
string output = result.GetProperty("standardOutput").GetProperty("content").GetString()!;

if (exit != 0)
{
    // curl refused the type itself; its message is on standard error.
    Console.WriteLine($"curl exit {exit}: {result.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
}
else if (which == "text-answer")
{
    // The text form: a header line that counts the answers, then a tab-separated line per record.
    string[] lines = output.Split('\n');
    Console.WriteLine(lines.Single(l => l.StartsWith(";; flags:")));
    string[] records = lines.SkipWhile(l => l != ";; ANSWER SECTION:").Skip(1).TakeWhile(l => l != "").ToArray();
    Console.WriteLine($"first: {records[0].Split('\t')[3]}, last: {records[^1].Split('\t')[3]}");
}
else
{
    // The JSON form: curl-mcp's output is itself the JSON the server answered.
    using JsonDocument answer = JsonDocument.Parse(output);
    JsonElement[] records = answer.RootElement.GetProperty("answer").EnumerateArray().ToArray();
    Console.WriteLine($"{answer.RootElement.GetProperty("status")}: {records.Length} records, curl exit {exit}");
    foreach (JsonElement record in records)
    {
        Console.WriteLine($"{record.GetProperty("name")} {record.GetProperty("type")}");
    }
}

// The exit code says whether the last call was a tool error.
Console.WriteLine($"tool error: {(last.IsError == true ? "yes" : "no")}");
return last.IsError == true ? 1 : 0;
