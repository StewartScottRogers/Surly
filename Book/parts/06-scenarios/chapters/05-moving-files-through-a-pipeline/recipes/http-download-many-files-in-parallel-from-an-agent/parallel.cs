// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves fifty files from surl-mcp with four connections allowed per address, fetches them
// all at once with curl-mcp's curl tool, and checks every file it saved against what it served.
// Run: dotnet run parallel.cs -- <case>
//   capped    --parallel --parallel-max 4 (the default)
//   uncapped  --parallel alone, up to 50 transfers at once
//   no-fail   --parallel alone and no -f
//   retry     --parallel alone, with --retry 10 --retry-delay 1
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "capped";

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

// surl-mcp: fifty files of 50,000 characters, and surl's own limit of four connections per address.
var files = new Dictionary<string, string>();
for (int i = 1; i <= 50; i++)
{
    files[$"f{i}.txt"] = new string((char)('a' + i % 26), 49_999) + "\n";
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--max-connections-per-address", "4" },
    ["files"] = files,
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp: one curl command line for all fifty, saved under out/ by the number the glob matched.
string folder = which == "capped" ? "out" : $"out-{which}";
var line = new List<string> { "-s", "--parallel" };
if (which != "no-fail")
{
    line.Add("-f");
}

if (which == "capped")
{
    line.AddRange(["--parallel-max", "4"]);
}

if (which == "retry")
{
    line.AddRange(["--retry", "10", "--retry-delay", "1"]);
}

line.AddRange(["--create-dirs", "-o", $"{folder}/f#1.txt", "-w", "%{http_code}\\n", url + "f[1-50].txt"]);
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = line });
using JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
string[] codes = (answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString() ?? "")
    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
Console.WriteLine($"curl: exit {answer.RootElement.GetProperty("exitCode")}, tool error: {(ran.IsError == true ? "yes" : "no")}");
Console.WriteLine($"200: {codes.Count(c => c == "200")}, 503: {codes.Count(c => c == "503")}");

// The check: every file saved is the file served, byte for byte.
int whole = files.Count(f => File.Exists(Path.Combine(here, folder, f.Key)) && File.ReadAllText(Path.Combine(here, folder, f.Key)) == f.Value);
Console.WriteLine($"saved and whole: {whole} of 50");
return whole == 50 ? 0 : 1;
