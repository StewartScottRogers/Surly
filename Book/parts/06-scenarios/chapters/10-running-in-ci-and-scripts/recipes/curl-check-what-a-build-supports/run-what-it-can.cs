// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Before a run, an agent asks curl-mcp's version tool which protocols and features this curl has,
// runs each test whose needs it meets against a surl-mcp server, and skips the rest, saying why.
// Run: dotnet run run-what-it-can.cs [-- all|missing-file|needs=<feature>]
//   all              every test as written (the default)
//   missing-file     the page test's server lacks its file, so that test fails
//   needs=<feature>  the IPv6 test needs this feature instead of IPv6
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "all";

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

// What this curl has: version answers in text, as JSON.
CallToolResult versionResult = await curl.CallToolAsync("version", new Dictionary<string, object?>());
JsonElement version = JsonDocument.Parse(versionResult.Content.OfType<TextContentBlock>().Single().Text).RootElement;
var protocols = version.GetProperty("protocols").EnumerateArray().Concat(version.GetProperty("extendedProtocols").EnumerateArray())
    .Select(p => p.GetString()!).ToHashSet();
var features = version.GetProperty("features").EnumerateArray().Select(f => f.GetString()!).ToHashSet();
Console.WriteLine($"curl {version.GetProperty("version")}: {protocols.Count} protocols, {features.Count} features");

// The tests: a name, the scheme it fetches, the feature it needs (if any), where surl listens,
// the file it serves, the path fetched and the text the answer must hold.
(string Name, string Scheme, string? Feature, string Listen, string File, string Fetch, string Expect)[] tests =
[
    ("page", "http", null, "http://127.0.0.1:0", "hello.txt", "hello.txt", "hello"),
    ("page over IPv6", "http", which.StartsWith("needs=") ? which["needs=".Length..] : "IPv6", "http://[::1]:0", "hello.txt", "hello.txt", "hello"),
    ("definition", "dict", null, "dict://127.0.0.1:0", "surl", "d:surl", "a server"),
    ("block", "tftp", null, "tftp://127.0.0.1:0", "hello.txt", "hello.txt", "hello"),
    ("stream", "rtmp", null, "rtmp://127.0.0.1:0", "clip", "clip", ""),
];

int failed = 0;
foreach (var test in tests)
{
    // Skip what this curl cannot do, and say which need was missing.
    if (!protocols.Contains(test.Scheme))
    {
        Console.WriteLine($"skip {test.Name}: curl has no {test.Scheme}");
        continue;
    }

    if (test.Feature is not null && !features.Contains(test.Feature))
    {
        Console.WriteLine($"skip {test.Name}: curl has no {test.Feature}");
        continue;
    }

    // Run it: a server holding the test's file (or, for missing-file, not), then one transfer.
    string content = test.File == "surl" ? "a server that answers curl\n" : "hello\n";
    bool withFile = !(which == "missing-file" && test.Name == "page");
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { test.Listen },
        ["files"] = withFile ? new Dictionary<string, string> { [test.File] = content } : new Dictionary<string, string>(),
    });
    string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + test.Fetch });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string body = answer.GetProperty("body").GetProperty("content").GetString() ?? "";
    bool passed = fetched.IsError != true && answer.GetProperty("statusCode").GetInt32() is 0 or 200 && body.Contains(test.Expect);
    Console.WriteLine($"{(passed ? "pass" : "FAIL")} {test.Name}: status {answer.GetProperty("statusCode")}");
    failed += passed ? 0 : 1;
}

// What surl recorded: the servers this host started, one per test that ran, and their exchanges.
JsonElement servers = (await surl.CallToolAsync("list_servers", new Dictionary<string, object?>())).StructuredContent!.Value;
foreach (JsonElement server in servers.GetProperty("servers").EnumerateArray())
{
    Console.WriteLine($"server {server.GetProperty("listen_urls")[0]}: {server.GetProperty("exchange_count")} exchange(s)");
}

return failed == 0 ? 0 : 1;
