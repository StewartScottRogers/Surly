// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent stands up a small site with surl-mcp's start_server and checks each file's
// Content-Type with curl-mcp's transfer, over the official MCP SDK.
// Run: dotnet run serve-site.cs [-- path ...]   (default: every file of the site)
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// The site: text files in files, the image's bytes in files_base64 (a PNG signature).
var text = new Dictionary<string, string>
{
    ["index.html"] = "<!doctype html><link rel=stylesheet href=css/site.css><img src=img/logo.png>\n",
    ["css/site.css"] = "body { margin: 0; }\n",
    ["js/app.js"] = "console.log('loaded');\n",
    ["img/icon.svg"] = "<svg xmlns='http://www.w3.org/2000/svg'/>\n",
    ["data/menu.json"] = "{\"items\": []}\n",
    ["downloads/notes.xyz"] = "an extension surl does not know\n",
};
var binary = new Dictionary<string, string> { ["img/logo.png"] = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) };
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = text,
    ["files_base64"] = binary,
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// One transfer per path; curl-mcp answers in text, as JSON, with the headers as name and value pairs.
string[] paths = args.Length > 0 ? args : ["index.html", "css/site.css", "js/app.js", "img/logo.png", "img/icon.svg", "data/menu.json", "downloads/notes.xyz"];
bool anyError = false;
foreach (string path in paths)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + path });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string type = answer.GetProperty("headers").EnumerateArray()
        .Where(h => h.GetProperty("name").GetString()!.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
        .Select(h => h.GetProperty("value").GetString()).FirstOrDefault() ?? "(none)";
    JsonElement body = answer.GetProperty("body");
    Console.WriteLine($"/{path} {answer.GetProperty("statusCode")} {type} isBase64: {body.GetProperty("isBase64").GetRawText()}");
    if (path == "img/logo.png")
    {
        Console.WriteLine($"  same bytes: {body.GetProperty("content").GetString() == binary[path]}");
    }

    anyError |= result.IsError == true;
}

Console.WriteLine($"tool error: {(anyError ? "yes" : "no")}");
return anyError ? 1 : 0;
