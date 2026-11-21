// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp server refuse one account's logins, runs curl with a retry option
// through curl-mcp, and counts from the server's events how many times the client tried.
// Run: dotnet run login-once.cs [-- careful|hammering|lifted]
//   careful    curl -f --retry 3 (the default)
//   hammering  curl -f --retry 3 --retry-all-errors
//   lifted     the careful client after remove_fault
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "careful";

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

// One server, two accounts, one file.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "app:s3cret", "--user", "other:o-secret" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";

// The rule: every login of app is refused, whatever its password.
CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
{
    ["server_id"] = id, ["fault"] = "refuse-login;account=app",
});
string fault = added.StructuredContent!.Value.GetProperty("fault_id").GetString()!;
Console.WriteLine($"add_fault: {fault}");
if (mode == "lifted")
{
    await surl.CallToolAsync("remove_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault_id"] = fault });
    Console.WriteLine($"remove_fault: {fault}");
}

// The client under test: curl with --retry, and for the hammering client --retry-all-errors.
string[] retry = mode == "hammering" ? ["--retry", "3", "--retry-delay", "1", "--retry-all-errors"] : ["--retry", "3", "--retry-delay", "1"];
Console.WriteLine($"app: {await Fetch("app:s3cret", retry)}");
Console.WriteLine($"other: {await Fetch("other:o-secret", retry)}");

// What the server saw: every login it judged for app, and how many the rule refused.
CallToolResult found = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" }, ["account"] = "app",
});
var logins = found.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToList();
int byRule = logins.Count(l => l.GetProperty("is_refused_by_fault").GetBoolean());
Console.WriteLine($"logins judged for app: {logins.Count}, refused by the rule: {byRule}");
return 0;

// curl-mcp's curl tool: -f makes a 401 exit 22; -w prints the status and the retries made.
async Task<string> Fetch(string login, string[] options)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "-f", "--digest", "-u", login, "-o", "hello.out", "-w", "%{http_code} %{num_retries}" }.Concat(options).Append(url).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
    return $"{output}, exit {answer.RootElement.GetProperty("exitCode").GetInt32()}";
}
