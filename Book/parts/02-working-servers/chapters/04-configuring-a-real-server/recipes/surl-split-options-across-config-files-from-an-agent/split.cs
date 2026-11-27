// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent layers a base set of surl options, a set for one test and a last set of overrides, and starts a surl-mcp
// server from them. surl-mcp takes no config file, so each layer is a list of arguments, as a file's lines would be.
// Run: dotnet run split.cs [-- SCENARIO]
//   both (the default), second-account, login-needed, later-layer-wins, under-the-limit, ftp-limit, ftp-same-store,
//   order-matters, override-after-wins, override-before-loses, no-allow-after, config-refused, typo
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "both";

string here = Directory.GetCurrentDirectory();
File.WriteAllText("up.txt", "0123456789abcdefghij\n");
File.WriteAllText("mid.txt", "1234567\n");
// Both hosts start at once: each takes seconds to come up on a loaded machine.
Task<McpClient> surlStart = McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl-mcp", Command = "surl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["SURL_MCP_ROOT"] = here },
}));
Task<McpClient> curlStart = McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "curl-mcp", Command = "curl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["CURL_MCP_ROOT"] = here },
}));
await using McpClient surl = await surlStart;
await using McpClient curl = await curlStart;


// The layers: what every test shares, what this test changes, and what the agent decides last.
string[] baseLayer = ["--user", "tester:secret", "--allow-plaintext-auth", "--allow-uploads", "--max-filesize", "100"];
string[] testLayer = ["--max-filesize", "10", "--user", "second:pw"];
string[] last = scenario switch
{
    "override-after-wins" or "override-before-loses" => ["--max-filesize", "5"],
    "no-allow-after" => ["--no-allow-uploads"],
    "config-refused" => ["-K", "base.conf"],
    "typo" => ["--max-filesise", "20"],
    _ => [],
};
string[] arguments = scenario switch
{
    "order-matters" => [.. testLayer, .. baseLayer],
    "override-before-loses" => [.. last, .. baseLayer, .. testLayer],
    _ => [.. baseLayer, .. testLayer, .. (scenario == "override-before-loses" ? [] : last)],
};

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "ftp://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "from the store\n" },
});

// A refused layer is a tool error with surl's own words; nothing listens.
if (started.IsError == true)
{
    string message = started.Content.OfType<TextContentBlock>().First().Text;
    Console.WriteLine("tool error: " + message.Split('\n').First(l => l.Contains("-K") || l.Contains("--max-filesise")).Trim());
    return 0;
}
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string http = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string ftp = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

switch (scenario)
{
    case "both": Console.WriteLine($"tester: {await Curl("-s", "-S", "-u", "tester:secret", http + "hello.txt")}"); break;
    case "second-account": Console.WriteLine($"second: {await Curl("-s", "-S", "-u", "second:pw", http + "hello.txt")}"); break;
    case "login-needed": Console.WriteLine($"no login: {await Curl("-s", "-o", "copy.txt", "-w", "%{http_code}", http + "hello.txt")}"); break;
    case "later-layer-wins" or "order-matters": Console.WriteLine($"21 bytes: {await Put("up.txt")}"); break;
    case "under-the-limit" or "override-after-wins" or "override-before-loses" or "no-allow-after": Console.WriteLine($"8 bytes: {await Put("mid.txt")}"); break;
    case "ftp-limit": Console.WriteLine($"ftp: {await Curl("-s", "-S", "-u", "tester:secret", "-T", "up.txt", ftp + scenario + ".txt")}"); break;
    case "ftp-same-store":
        Console.WriteLine($"ftp: {await Curl("-s", "-S", "-u", "tester:secret", "-T", "mid.txt", ftp + scenario + ".txt")}");
        CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/" + scenario + ".txt" });
        Console.Write($"the store holds: {read.StructuredContent!.Value.GetProperty("text").GetString()}");
        break;
}
return 0;

// An HTTP PUT as tester: the status the server answered.
async Task<string> Put(string file) =>
    await Curl("-s", "-S", "-u", "tester:secret", "-T", file, "-o", "copy.txt", "-w", "%{http_code}", http + scenario + ".txt");

// curl-mcp's curl tool: the file's text or the status code when it succeeded, else the exit code.
async Task<string> Curl(params string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int code = answer.RootElement.GetProperty("exitCode").GetInt32();
    return code == 0 ? answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd() : $"exit code {code}";
}
