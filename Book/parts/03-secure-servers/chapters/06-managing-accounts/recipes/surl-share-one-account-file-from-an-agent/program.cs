// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent's side of the conversation, as a C# file-based app: `dotnet run program.cs -- alice s3cret
// [users-file]`. It starts surl-mcp and curl-mcp over standard input and output with the official MCP
// SDK, logs in on four protocols with one account, and prints what the recipe's claims check.
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string login = $"{args[0]}:{args[1]}";
string file = args.Length > 2 ? args[2] : "users.txt";

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

// surl-mcp: one server, four TLS listeners, one account file. --user-file and the certificate are
// surl's own options in `arguments`; the files are read from the confined folder.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0", "ftps://127.0.0.1:0", "imaps://127.0.0.1:0", "mqtts://127.0.0.1:0" },
    ["arguments"] = new[] { "--user-file", file, "--cert", "server.pem", "--key", "server.key" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
if (started.IsError == true)
{
    Console.WriteLine($"start refused: {started.StructuredContent?.GetProperty("exit_code")} {started.Content.OfType<TextContentBlock>().Single().Text}");
    return 1;
}

JsonElement bound = started.StructuredContent!.Value.GetProperty("bound_urls");
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;

// curl-mcp's curl tool takes curl's own arguments: one call, one protocol, the same -u.
await Curl("https", "-f", "-u", login, bound[0].GetString()! + "hello.txt");
await Curl("ftps", "-u", login, bound[1].GetString()! + "hello.txt");
await Curl("imaps", "-u", login, "-X", "STATUS INBOX (MESSAGES)", bound[2].GetString()!);
await Curl("mqtts", "-u", login, "-d", "on", bound[3].GetString()! + "t/lamp");

// surl-mcp records every login it judges: the account and whether it was accepted, per exchange.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id,
    ["kinds"] = new[] { "login_judged" },
});
JsonElement judged = events.StructuredContent!.Value.GetProperty("events");
var seen = judged.EnumerateArray()
    .Select(e => $"{e.GetProperty("account").GetString()} {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}")
    .Distinct().Order();
Console.WriteLine($"logins judged: {judged.GetArrayLength()}; {string.Join(", ", seen)}");
return 0;

// One curl run: prints the protocol, the exit code, then the first line of standard output or error.
async Task Curl(string label, params string[] curlArgs)
{
    string[] all = ["-s", "-S", "--cacert", "server.pem", .. curlArgs];
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = all });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    string text = Content(root, "standardOutput") ?? Content(root, "standardError") ?? "";
    Console.WriteLine($"{label}: exit {root.GetProperty("exitCode").GetInt32()} {text.Trim()}".TrimEnd());
}

static string? Content(JsonElement root, string stream) =>
    root.GetProperty(stream).ValueKind == JsonValueKind.Object && root.GetProperty(stream).GetProperty("content").GetString() is { Length: > 0 } s ? s : null;
