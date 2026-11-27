// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds keys in a running surl Redis store through surl-mcp, reads the store with its
// tools, and reads and writes keys with curl through curl-mcp. Run: dotnet run keys.cs -- <case>
//   solution          set_key, read_key, list_keys, then curl GET over redis://
//   client-needs-login curl GET without a login
//   pattern-and-pages list_keys with a pattern, a limit and after_key
//   curl-writes       curl SET, then read_key
//   database          set_key in database 2, curl GET from database 2, list_keys in database 0
//   bytes             set_key with value_base64, then read_key
//   expires           set_key with expire_ms, wait past it, then read_key and curl GET
//   no-such-key       read_key of a key nobody set
//   no-redis-listener set_key on a server with only an HTTP listener
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// A Redis listener on a free port and one account, alice, whose password may cross it in clear;
// in no-redis-listener, an HTTP listener instead.
string listen = which == "no-redis-listener" ? "http://127.0.0.1:0" : "redis://127.0.0.1:0";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { listen },
    ["arguments"] = new[] { "--user", "alice:s3cret", "--allow-plaintext-auth" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string redis = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "solution":
        await SetKey(new() { ["key"] = "greeting", ["value"] = "hello" });
        await ReadKey("greeting");
        await ListKeys(new());
        await Curl("-u", "alice:s3cret", redis + "GET/greeting");
        break;
    case "client-needs-login":
        await SetKey(new() { ["key"] = "greeting", ["value"] = "hello" });
        await Curl("-S", redis + "GET/greeting");
        break;
    case "pattern-and-pages":
        await SetKey(new() { ["key"] = "user:2", ["value"] = "bob" });
        await SetKey(new() { ["key"] = "greeting", ["value"] = "hello" });
        await SetKey(new() { ["key"] = "user:1", ["value"] = "alice" });
        await ListKeys(new() { ["pattern"] = "user:*" });
        await ListKeys(new() { ["limit"] = 1 });
        await ListKeys(new() { ["limit"] = 1, ["after_key"] = "greeting" });
        break;
    case "curl-writes":
        await SetKey(new() { ["key"] = "greeting", ["value"] = "hello" });
        await Curl("-u", "alice:s3cret", redis + "SET/greeting", "-d", "changed");
        await ReadKey("greeting");
        break;
    case "database":
        await SetKey(new() { ["key"] = "greeting", ["value"] = "hello", ["database"] = 2 });
        await Curl("-u", "alice:s3cret", redis + "2/GET/greeting");
        await ListKeys(new());
        break;
    case "bytes":
        await SetKey(new() { ["key"] = "raw", ["value_base64"] = "AAEC" });
        await ReadKey("raw");
        break;
    case "expires":
        await SetKey(new() { ["key"] = "session", ["value"] = "abc", ["expire_ms"] = 1000 });
        await Task.Delay(3000);
        await ReadKey("session");
        await Curl("-u", "alice:s3cret", redis + "GET/session");
        break;
    case "no-such-key":
        await ReadKey("missing");
        break;
    case "no-redis-listener":
        await SetKey(new() { ["key"] = "greeting", ["value"] = "hello" });
        break;
    default:
        Console.Error.WriteLine($"unknown case {which}");
        return 2;
}

return 0;

// set_key writes into the store as a client's SET would, with no client and no login;
// a server with no Redis listener has no store, and the call is a tool error.
async Task SetKey(Dictionary<string, object?> arguments)
{
    arguments["server_id"] = server;
    CallToolResult result = await surl.CallToolAsync("set_key", arguments);
    if (result.IsError == true)
    {
        Console.WriteLine($"set_key {arguments["key"]}: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
        return;
    }

    JsonElement set = result.StructuredContent!.Value;
    Console.WriteLine($"set_key: {set.GetProperty("key")} in database {set.GetProperty("database")}, {set.GetProperty("byte_count")} bytes");
}

// read_key answers the value as text, or as base64 when it is not UTF-8 text.
async Task ReadKey(string key)
{
    CallToolResult result = await surl.CallToolAsync("read_key", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["key"] = key,
    });
    if (result.IsError == true)
    {
        Console.WriteLine($"read_key {key}: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
        return;
    }

    JsonElement value = result.StructuredContent!.Value;
    string shown = value.TryGetProperty("text", out JsonElement text) ? $"text {text.GetRawText()}" : $"base64 {value.GetProperty("base64")}";
    Console.WriteLine($"read_key {key}: {value.GetProperty("type")}, {value.GetProperty("byte_count")} bytes, {shown}");
}

// list_keys answers the keys in order, a page at a time; has_more says whether a page follows.
async Task ListKeys(Dictionary<string, object?> arguments)
{
    arguments["server_id"] = server;
    CallToolResult result = await surl.CallToolAsync("list_keys", arguments);
    JsonElement list = result.StructuredContent!.Value;
    IEnumerable<string> keys = list.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("key").GetString()!);
    Console.WriteLine($"list_keys: [{string.Join(", ", keys)}], has_more {list.GetProperty("has_more").GetRawText()}");
}

// The curl tool runs curl's own arguments; -s keeps the progress meter out of the answer.
async Task Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = arguments.Prepend("-s").ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    string error = root.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    Console.WriteLine($"curl exit {root.GetProperty("exitCode")}, output "
        + root.GetProperty("standardOutput").GetProperty("content").GetRawText() + (error == "" ? "" : $", error {error}"));
}
