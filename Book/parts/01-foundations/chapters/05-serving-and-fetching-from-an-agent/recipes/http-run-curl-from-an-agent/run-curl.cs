// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs curl through curl-mcp's curl tool against a server surl-mcp started, and prints the answer.
// Run: dotnet run run-curl.cs -- <case>
//   fetch          curl -s URL/hello.txt (the default)
//   status         curl -s -w "%{http_code}" URL/missing.txt
//   fail           curl -f -s URL/missing.txt
//   show-error     curl -f -sS URL/missing.txt
//   write          curl -s -o hello.txt -w "%{http_code}" URL/hello.txt
//   outside-root   curl -s -o ../hello.txt URL/hello.txt
//   unknown        curl --no-such-option URL/hello.txt
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

// Something for curl to talk to: a server on a free port with one file in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl's arguments, one list item each, without the program name: no shell, no quoting.
string[] curlArgs = which switch
{
    "status" => ["-s", "-w", "%{http_code}", url + "missing.txt"],
    "fail" => ["-f", "-s", url + "missing.txt"],
    "show-error" => ["-f", "-sS", url + "missing.txt"],
    "write" => ["-s", "-o", "hello.txt", "-w", "%{http_code}", url + "hello.txt"],
    "outside-root" => ["-s", "-o", "../hello.txt", url + "hello.txt"],
    "unknown" => ["--no-such-option", url + "hello.txt"],
    _ => ["-s", url + "hello.txt"],
};
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });

// curl-mcp answers in text, as JSON. Each stream is null when curl never ran.
JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode").GetRawText()}");
foreach (string stream in new[] { "standardOutput", "standardError" })
{
    JsonElement s = answer.GetProperty(stream);
    Console.WriteLine($"{stream}: {(s.ValueKind == JsonValueKind.Null ? "null" : s.GetProperty("content").GetRawText())}");
}

JsonElement failure = answer.GetProperty("failure");
Console.WriteLine($"failure: {(failure.ValueKind == JsonValueKind.Null ? "null" : $"{failure.GetProperty("kind")}, exitCode {failure.GetProperty("exitCode").GetRawText()}")}");
if (failure.ValueKind != JsonValueKind.Null)
{
    Console.WriteLine(failure.GetProperty("text"));
}

if (which == "write")
{
    Console.Write($"hello.txt holds: {File.ReadAllText("hello.txt")}");
}

// The exit code says whether the call was a tool error.
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
