// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent saves files from a server surl-mcp started into folders inside CURL_MCP_ROOT,
// through curl-mcp's curl tool, over the official MCP SDK.
// Run: dotnet run save.cs -- <case>
//   save          --create-dirs --output-dir downloads, -o one name, -O the other (the default)
//   outside-dir   --output-dir ..
//   outside-o     -o ../guide.txt
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "save";

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

// A server on a free port with two files in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string>
    {
        ["docs/guide.txt"] = "the guide\n",
        ["reports/2026/summary.txt"] = "the summary\n",
    },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl's arguments, one item each; -w names each file written.
string[] curlArgs = which switch
{
    "outside-dir" => ["-s", "--output-dir", "..", "-O", url + "docs/guide.txt"],
    "outside-o" => ["-s", "-o", "../guide.txt", url + "docs/guide.txt"],
    _ => ["-s", "--create-dirs", "--output-dir", "downloads", "-w", "%{filename_effective}\n",
          "-o", "handbook.txt", url + "docs/guide.txt", "-O", url + "reports/2026/summary.txt"],
};
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });

// curl-mcp answers in text, as JSON; the streams are null when curl never ran.
JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode").GetRawText()}");
JsonElement output = answer.GetProperty("standardOutput");
Console.Write(output.ValueKind == JsonValueKind.Null ? "standardOutput: null\n" : output.GetProperty("content").GetString());
JsonElement failure = answer.GetProperty("failure");
if (failure.ValueKind != JsonValueKind.Null)
{
    Console.WriteLine($"failure: {failure.GetProperty("kind")}");
    Console.WriteLine(failure.GetProperty("text"));
}

// What the save landed on disk, read back from the root.
foreach (string file in which == "save" ? new[] { "downloads/handbook.txt", "downloads/summary.txt" } : [])
{
    Console.Write($"{file} holds: {File.ReadAllText(file)}");
}

Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
