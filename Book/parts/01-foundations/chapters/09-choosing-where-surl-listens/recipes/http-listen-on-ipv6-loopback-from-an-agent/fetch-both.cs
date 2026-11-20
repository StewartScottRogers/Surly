// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a file on IPv4 and IPv6 loopback with surl-mcp and fetches it from each with curl-mcp.
// Run: dotnet run fetch-both.cs -- <case>
//   both         listen on 127.0.0.1 and [::1], fetch from each (the default)
//   v6-only      listen on [::1] only, then fetch from 127.0.0.1 at the same port
//   no-brackets  ask for a listen URL with the IPv6 address bare
//   any-address  ask for a listen URL on [::], every address
//   bare-client  fetch from an IPv6 URL written without brackets
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "both";

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

string[] listen = which switch
{
    "v6-only" => ["http://[::1]:0"],
    "no-brackets" => ["http://::1:0"],
    "any-address" => ["http://[::]:0"],
    _ => ["http://127.0.0.1:0", "http://[::1]:0"],
};

// surl-mcp: the server, one listen URL for each address, holding one file in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = listen,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
if (started.IsError == true)
{
    // A refusal: structured content with surl's exit code, or a text that says why.
    if (started.StructuredContent?.TryGetProperty("exit_code", out JsonElement code) == true)
    {
        Console.WriteLine($"exit_code: {code} ({started.StructuredContent.Value.GetProperty("exit_code_name")})");
    }

    if (started.StructuredContent?.TryGetProperty("refused", out JsonElement refused) == true)
    {
        Console.WriteLine($"refused: {refused}");
    }

    Console.WriteLine("tool error: yes");
    return 1;
}

string[] bound = started.StructuredContent!.Value.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();
foreach (string url in bound)
{
    Console.WriteLine($"bound {url}");
}

string[] targets = which switch
{
    "v6-only" => [$"http://127.0.0.1:{new Uri(bound[0]).Port}/hello.txt"],
    "bare-client" => [$"http://::1:{new Uri(bound[0]).Port}/hello.txt"],
    _ => bound.Select(url => url + "hello.txt").ToArray(),
};

// curl-mcp: one request for each. It answers in text, as JSON, with the address in info.primaryIp.
int problems = 0;
foreach (string target in targets)
{
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = target });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement failure = answer.GetProperty("failure");
    if (failure.ValueKind == JsonValueKind.Null)
    {
        Console.WriteLine($"{answer.GetProperty("statusCode")} {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()} from {answer.GetProperty("info").GetProperty("primaryIp").GetString()}");
    }
    else
    {
        string kind = failure.GetProperty("kind").GetString()!;
        JsonElement exitCode = failure.GetProperty("exitCode");
        Console.WriteLine(exitCode.ValueKind == JsonValueKind.Null
            ? $"failure: {kind}: {failure.GetProperty("text").GetString()}"
            : $"failure: {kind} {exitCode}");
        Console.WriteLine($"tool error: {(fetched.IsError == true ? "yes" : "no")}");
        problems++;
    }
}

return problems == 0 ? 0 : 1;
