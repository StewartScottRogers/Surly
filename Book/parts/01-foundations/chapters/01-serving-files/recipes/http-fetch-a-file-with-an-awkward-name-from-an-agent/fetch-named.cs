// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches files with awkward names: surl-mcp serves them, curl-mcp builds each URL and fetches it.
// Run: dotnet run fetch-named.cs -- <how> [name]
//   how    build (the default): build_url with the UrlEncode flag; plain: build_url without it;
//          raw: the name appended to the server's URL as given
//   name   one file name to ask for; with none, all three awkward names
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string how = args.Length > 0 ? args[0] : "build";
string[] names = args.Length > 1 ? [args[1]] : ["my file.txt", "café.txt", "a#b.txt"];

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

// surl-mcp: the real names, and a file called a, to see where a raw # leads.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string>
    {
        ["my file.txt"] = "spaced\n", ["café.txt"] = "accented\n", ["a#b.txt"] = "hashed\n", ["a"] = "just a\n",
    },
});
string bound = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

int problems = 0;
foreach (string name in names)
{
    string url = bound + name;
    if (how != "raw")
    {
        // curl-mcp's build_url sets URL parts; UrlEncode (libcurl's CURLU_URLENCODE) encodes the path.
        var build = new Dictionary<string, object?>
        {
            ["scheme"] = "http", ["host"] = "127.0.0.1", ["port"] = new Uri(bound).Port.ToString(), ["path"] = "/" + name,
        };
        if (how == "build") { build["flags"] = new[] { "UrlEncode" }; }
        CallToolResult built = await curl.CallToolAsync("build_url", build);
        url = JsonDocument.Parse(built.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("url").GetString()!;
    }

    // curl-mcp's transfer: one request, answered in text, as JSON.
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string path = url[bound.Length..];
    JsonElement failure = answer.GetProperty("failure");
    if (failure.ValueKind == JsonValueKind.Null)
    {
        int status = answer.GetProperty("statusCode").GetInt32();
        Console.WriteLine($"{path} -> {status} {answer.GetProperty("body").GetProperty("content").GetString()}".TrimEnd());
        if (status != 200) { problems++; }
    }
    else
    {
        Console.WriteLine($"{path} -> failure {failure.GetProperty("kind").GetString()} {failure.GetProperty("exitCode").GetRawText()}");
        problems++;
    }

    if (fetched.IsError == true) { Console.WriteLine("tool error: yes"); }
}

return problems == 0 ? 0 : 1;
