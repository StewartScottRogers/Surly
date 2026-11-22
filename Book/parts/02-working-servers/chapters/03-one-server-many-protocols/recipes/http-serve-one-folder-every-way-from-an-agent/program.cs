// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts one surl-mcp server on six schemes over one store, fetches the same file over each
// with curl-mcp's transfer, and compares the bytes with what read_file says the store holds.
// Run: dotnet run program.cs -- <case>
//   six           start with --allow-anonymous and fetch hello.txt six ways (the default)
//   warning       the same start, printing the log lines it answered
//   no-anonymous  start without --allow-anonymous and fetch over FTP
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "six";

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

// surl-mcp: one server, six listen URLs on free ports, one file in memory.
string[] schemes = ["http", "ftp", "tftp", "gopher", "dict", "ws"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = schemes.Select(s => $"{s}://127.0.0.1:0").ToArray(),
    ["arguments"] = which == "no-anonymous" ? Array.Empty<string>() : new[] { "--allow-anonymous" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string[] bound = server.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();

if (which == "warning")
{
    foreach (JsonElement line in server.GetProperty("log_lines").EnumerateArray())
    {
        Console.WriteLine(line.GetString());
    }

    return 0;
}

// What the store holds, read directly rather than over a protocol.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
{
    ["server_id"] = server.GetProperty("server_id").GetString(),
    ["path"] = "/hello.txt",
});
string stored = read.StructuredContent!.Value.GetProperty("text").GetString()!;
Console.WriteLine($"read_file: {read.StructuredContent!.Value.GetProperty("byte_count")} bytes");

// Gopher puts item type 0 before the path; DICT looks up the headword.
string[] paths = ["hello.txt", "hello.txt", "hello.txt", "0/hello.txt", "d:hello.txt", "hello.txt"];
bool anyError = false;
for (int i = 0; i < schemes.Length; i++)
{
    if (which == "no-anonymous" && schemes[i] != "ftp")
    {
        continue;
    }

    // curl-mcp answers in text, as JSON.
    CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = bound[i] + paths[i] });
    JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    anyError |= fetched.IsError == true;
    if (answer.GetProperty("failure").ValueKind != JsonValueKind.Null)
    {
        Console.WriteLine($"{schemes[i]}: failure {answer.GetProperty("failure").GetProperty("text")}, tool error {(fetched.IsError == true ? "yes" : "no")}");
        continue;
    }

    string content = answer.GetProperty("body").GetProperty("content").GetString()!;
    string verdict = content == stored ? "same as read_file"
        : content.Contains("\r\n" + stored.Replace("\n", "\r\n") + ".\r\n") ? "a DICT reply holding it"
        : "different";
    Console.WriteLine($"{schemes[i]}: statusCode {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("totalBytes")} bytes, {verdict}");
}

return anyError ? 1 : 0;
