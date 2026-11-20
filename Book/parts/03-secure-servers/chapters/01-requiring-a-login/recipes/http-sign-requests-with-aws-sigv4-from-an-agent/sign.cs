// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a surl-mcp server that takes AWS Signature Version 4 alone, and signs requests to
// it through curl-mcp.
// Run: dotnet run sign.cs [-- fetch|wrong|unknown|unsigned|password|scope|missing|events|default|default-wrong]
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fetch";

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

// One account, an access key ID and its secret key; SigV4 alone, unless the scenario starts "default".
var serverArguments = new List<string> { "--user", "AKIAEXAMPLE:topsecret" };
if (!which.StartsWith("default")) serverArguments.AddRange(["--auth", "aws-sigv4"]);
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = serverArguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "fetch":
        Console.Write(await Curl("-s", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", "AKIAEXAMPLE:topsecret", url + "hello.txt"));
        break;
    case "wrong":
        Console.WriteLine(await Status("AKIAEXAMPLE:wrong"));
        break;
    case "unknown":
        Console.WriteLine(await Status("NOBODY:topsecret"));
        break;
    case "unsigned":
        // transfer sends no login at all: the answer is the status and the headers.
        CallToolResult bare = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt" });
        using (JsonDocument answer = JsonDocument.Parse(bare.Content.OfType<TextContentBlock>().Single().Text))
        {
            int challenges = answer.RootElement.GetProperty("headers").EnumerateArray()
                .Count(h => h.GetProperty("name").GetString()!.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"{answer.RootElement.GetProperty("statusCode").GetInt32()}; challenges: {challenges}");
        }
        break;
    case "password":
        Console.WriteLine($"Basic: {await Curl("-s", "-o", "hello.out", "-w", "%{http_code}", "-u", "AKIAEXAMPLE:topsecret", url + "hello.txt")}");
        Console.WriteLine($"Digest: {await Curl("-s", "-o", "hello.out", "-w", "%{http_code}", "--digest", "-u", "AKIAEXAMPLE:topsecret", url + "hello.txt")}");
        break;
    case "scope":
        Console.WriteLine(await Status("AKIAEXAMPLE:topsecret", "aws:amz:eu-west-1:ec2"));
        break;
    case "missing":
        Console.WriteLine(await Status("AKIAEXAMPLE:topsecret", path: "missing.txt"));
        break;
    case "events":
        await Status("AKIAEXAMPLE:topsecret");
        await Status("AKIAEXAMPLE:wrong");
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = server, ["kinds"] = new[] { "login_judged" } });
        foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
            Console.WriteLine($"{e.GetProperty("mechanism").GetString()} {e.GetProperty("account").GetString()}: {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
        break;
    case "default":
        Console.WriteLine(await Status("AKIAEXAMPLE:topsecret"));
        break;
    case "default-wrong":
        Console.WriteLine(await Curl("-s", "-D", "-", "-o", "hello.out", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", "AKIAEXAMPLE:wrong", url + "hello.txt") is var head
            ? $"{head.Split("\r\n")[0]}; Digest challenge: {head.Contains("WWW-Authenticate: Digest")}" : "");
        break;
}
return 0;

// curl-mcp's curl tool with the arguments given; the answer is what curl wrote to standard output.
async Task<string> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!;
}

// A signed request for a path; the answer is the status code.
Task<string> Status(string login, string scope = "aws:amz:us-east-1:s3", string path = "hello.txt") =>
    Curl("-s", "-o", "hello.out", "-w", "%{http_code}", "--aws-sigv4", scope, "-u", login, url + path);
