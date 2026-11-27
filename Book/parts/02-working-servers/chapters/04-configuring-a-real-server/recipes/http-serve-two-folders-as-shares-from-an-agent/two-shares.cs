// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves two folders as shares with surl-mcp, /docs readonly and /data writable, reads and uploads with
// curl-mcp, and starts a second server on the same folders. Run: dotnet run two-shares.cs -- MODE
//   serve            read a file from each share
//   readonly         upload into the readonly share, then look for the file
//   writable         upload into the other share, then read the folder on disk
//   second-readonly  a second server on the readonly folder, read from it
//   second-writable  a second server on the writable folder
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "serve";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The shares are folders in it.
string here = Directory.GetCurrentDirectory();
Directory.CreateDirectory("docs");
Directory.CreateDirectory("data");
File.WriteAllText("docs/guide.txt", "Read me first.\n");
File.WriteAllText("data/a.txt", "first\n");
File.WriteAllText("up.txt", "uploaded\n");
File.Delete("docs/up.txt");
File.Delete("data/up.txt");

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

// surl-mcp: --share is surl's own option, in surl's own words; the ";readonly" flag is part of its value.
(string url, string refused) = await Start(["--allow-anonymous", "--allow-uploads", "--share", "docs=docs;readonly", "--share", "data=data"]);

switch (which)
{
    case "serve":
        Console.WriteLine($"docs: {await Run("-s", url + "docs/guide.txt")}");
        Console.WriteLine($"data: {await Run("-s", url + "data/a.txt")}");
        break;
    case "readonly":
        Console.WriteLine($"upload: {await Run("-s", "-o", "up.out", "-w", "%{http_code}", "-T", "up.txt", url + "docs/up.txt")}");
        Console.WriteLine($"on disk: {File.Exists("docs/up.txt")}");
        break;
    case "writable":
        Console.WriteLine($"upload: {await Run("-s", "-o", "up.out", "-w", "%{http_code}", "-T", "up.txt", url + "data/up.txt")}");
        Console.WriteLine($"on disk: {File.ReadAllText("data/up.txt").TrimEnd()}");
        break;
    case "second-readonly":
    {
        (string second, string why) = await Start(["--share", "docs=docs;readonly"]);
        Console.WriteLine(second == "" ? $"second refused: {why}" : $"second docs: {await Run("-s", second + "docs/guide.txt")}");
        break;
    }
    case "second-writable":
    {
        (string second, string why) = await Start(["--share", "data=data"]);
        Console.WriteLine(second == "" ? $"second refused: {why}" : "second: started");
        break;
    }
    default:
        Console.Error.WriteLine($"unknown mode {which}");
        return 2;
}

return 0;

// start_server: the first bound URL, or the first line of the refusal when the tool answers an error.
async Task<(string, string)> Start(string[] arguments)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "http://127.0.0.1:0" },
        ["arguments"] = arguments,
    });
    if (started.IsError == true)
        return ("", started.Content.OfType<TextContentBlock>().Single().Text.Split('\n')[0]);
    return (started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!, "");
}

// The curl tool runs curl's own arguments, as the console would; its answer is JSON with the output.
async Task<string> Run(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd();
}
