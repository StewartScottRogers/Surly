// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches the same path from a surl-mcp server with curl-mcp's transfer tool and with its
// curl tool, and prints what each answer holds.
// Run: dotnet run compare-tools.cs -- <case>
//   hello    fetch hello.txt both ways (the default)
//   missing  fetch a path that is not there both ways, and with curl -f
//   save     save hello.txt to a file with the curl tool
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "hello";

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

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + (which == "missing" ? "missing.txt" : "hello.txt");

if (which == "save")
{
    // Only the curl tool writes to disk, and only inside CURL_MCP_ROOT.
    if (File.Exists(Path.Combine("downloads", "hello.txt"))) File.Delete(Path.Combine("downloads", "hello.txt"));
    JsonElement saved = await Curl("-s", "--create-dirs", "-o", "downloads/hello.txt", "-w", "%{http_code}", url);
    Console.WriteLine($"curl: exitCode {saved.GetProperty("exitCode")}, standardOutput {Quote(saved.GetProperty("standardOutput"))}");
    Console.WriteLine($"downloads/hello.txt: {Shown(File.ReadAllText(Path.Combine("downloads", "hello.txt")))}");
    return 0;
}

// transfer: one request as fields; the answer is the response, taken apart.
CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url });
JsonElement t = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string? type = t.GetProperty("headers").EnumerateArray()
    .Where(h => h.GetProperty("name").GetString() == "Content-Type").Select(h => h.GetProperty("value").GetString()).FirstOrDefault();
Console.WriteLine($"transfer: statusCode {t.GetProperty("statusCode")}, Content-Type {type ?? "none"}, body {Quote(t.GetProperty("body"))}, tool error {(fetched.IsError == true ? "yes" : "no")}");

// curl: a command line; the answer is what the command printed and its exit code.
JsonElement c = await Curl("-s", url);
Console.WriteLine($"curl: exitCode {c.GetProperty("exitCode")}, standardOutput {Quote(c.GetProperty("standardOutput"))}");
if (which == "missing")
{
    JsonElement f = await Curl("-s", "-f", "-w", "%{http_code}", url);
    Console.WriteLine($"curl -f: exitCode {f.GetProperty("exitCode")}, standardOutput {Quote(f.GetProperty("standardOutput"))}, failure {f.GetProperty("failure").GetProperty("text")}");
}

return 0;

async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    return JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

static string Quote(JsonElement output) => Shown(output.GetProperty("content").GetString()!);

// Text in quotes, a line end written as \n, so each answer stays on one line.
static string Shown(string text) => $"\"{text.Replace("\n", "\\n")}\"";
