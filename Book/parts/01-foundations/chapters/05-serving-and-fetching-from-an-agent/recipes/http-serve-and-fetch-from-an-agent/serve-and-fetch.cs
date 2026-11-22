// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a file with surl-mcp and fetches it with curl-mcp, over the official MCP SDK.
// Run: dotnet run serve-and-fetch.cs -- <case>
//   fetch         start a server on port 0 and fetch hello.txt from the URL it bound (the default)
//   headers       the same fetch, showing one of the response headers
//   missing       fetch a path the server does not have
//   port-in-use   start a second server on the URL the first one bound
//   not-loopback  start a server on 0.0.0.0
//   file-url      ask transfer for a file: URL
#:package ModelContextProtocol.Core@*

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

// surl-mcp: a server on a free port, holding one file in memory. It answers in structured content.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string url = server.GetProperty("bound_urls")[0].GetString()!;

CallToolResult result;
switch (which)
{
    case "port-in-use" or "not-loopback":
        string listen = which == "port-in-use" ? url : "http://0.0.0.0:0";
        result = await surl.CallToolAsync("start_server", new Dictionary<string, object?> { ["listen_urls"] = new[] { listen } });
        if (result.StructuredContent?.TryGetProperty("exit_code", out JsonElement code) == true)
        {
            Console.WriteLine($"exit_code: {code} ({result.StructuredContent.Value.GetProperty("exit_code_name")})");
        }

        Console.WriteLine(result.Content.OfType<TextContentBlock>().Single().Text);
        break;

    default:
        // curl-mcp: one request. It answers in text, as JSON.
        string target = which switch { "missing" => url + "missing.txt", "file-url" => "file:///etc/hosts", _ => url + "hello.txt" };
        result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = target });
        JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        JsonElement failure = answer.GetProperty("failure");
        if (which == "fetch")
        {
            Console.WriteLine($"listen_urls: {server.GetProperty("listen_urls")[0]}");
            Console.WriteLine($"bound_urls: {url}");
        }

        Console.WriteLine($"statusCode: {answer.GetProperty("statusCode").GetRawText()}");
        if (which == "headers")
        {
            JsonElement header = answer.GetProperty("headers").EnumerateArray().First(h => h.GetProperty("name").GetString() == "Content-Type");
            Console.WriteLine($"{header.GetProperty("name")}: {header.GetProperty("value")}");
        }
        else if (failure.ValueKind == JsonValueKind.Null)
        {
            Console.WriteLine($"failure: null");
            Console.WriteLine($"isBase64: {answer.GetProperty("body").GetProperty("isBase64").GetRawText()}");
            Console.Write(answer.GetProperty("body").GetProperty("content").GetString());
        }
        else
        {
            Console.WriteLine($"failure: {failure.GetProperty("kind")}");
            Console.WriteLine(failure.GetProperty("text"));
        }

        break;
}

// The exit code says whether the last call was a tool error.
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
