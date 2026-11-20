// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent sets a Redis key with an expiry through surl-mcp, reads it with surl-mcp and curl-mcp while it lives
// and after it has gone, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   expire     a key that lives six seconds, read before and after (the default)
//   no-expiry  a key with no expiry, then a limit put on it with curl's EXPIRE
//   bounds     expire_ms values set_key refuses
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "expire";

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

// A Redis listener on a free port and one account, alice, whose password may cross it in clear.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "redis://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "alice:s3cret", "--allow-plaintext-auth" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string redis = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "no-expiry":
        await SetKey(null);
        await ReadKey("before");
        await Curl("TTL", "session");
        await Curl("EXPIRE", "session", "1000");
        await Curl("TTL", "session");
        break;
    case "bounds":
        await SetKey(0);
        await SetKey(31536000001);
        await SetKey(1);
        break;
    default:
        // expire_ms is the key's life in milliseconds: set_key sets it as SET with PX would.
        await SetKey(6000);
        await ReadKey("before");
        await Curl("GET", "session");
        await Curl("TTL", "session");
        await Task.Delay(7000);
        await ReadKey("after");
        await Curl("GET", "session");
        await Curl("TTL", "session");
        break;
}

return 0;

async Task SetKey(long? expireMs)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = server, ["key"] = "session", ["value"] = "abc" };
    if (expireMs is not null)
    {
        arguments["expire_ms"] = expireMs;
    }

    string label = $"set_key expire_ms {(expireMs is null ? "none" : expireMs)}";
    try
    {
        CallToolResult result = await surl.CallToolAsync("set_key", arguments);
        Console.WriteLine(result.IsError == true
            ? $"{label}: tool error: {result.Content.OfType<TextContentBlock>().Single().Text}"
            : $"{label}: {result.StructuredContent!.Value.GetProperty("byte_count")} bytes");
    }
    catch (McpProtocolException refused)
    {
        // A value outside the tool's schema never reaches surl: the host answers an invalid-params error.
        Console.WriteLine($"{label}: refused: {refused.Message}");
    }
}

// read_key answers the value, and expires_at: a time while the key has a limit, null if it has none.
async Task ReadKey(string when)
{
    CallToolResult result = await surl.CallToolAsync("read_key", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["key"] = "session",
    });
    if (result.IsError == true)
    {
        Console.WriteLine($"{when}: read_key: error: {result.Content.OfType<TextContentBlock>().Single().Text}");
        return;
    }

    JsonElement key = result.StructuredContent!.Value;
    string expires = key.TryGetProperty("expires_at", out JsonElement at) && at.ValueKind != JsonValueKind.Null ? "a time" : "none";
    Console.WriteLine($"{when}: read_key: {key.GetProperty("text")}, expires_at {expires}");
}

// The curl tool runs curl's own arguments; the URL path is the Redis command. -s keeps the progress meter out.
async Task Curl(params string[] command)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-u", "alice:s3cret", redis + string.Join('/', command) },
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string shown = command[0] == "TTL" ? (int.Parse(output) > 0 ? "positive" : output.TrimEnd()) : output.TrimEnd();
    Console.WriteLine($"curl {command[0]}: exit {answer.GetProperty("exitCode")}, {(shown == "" ? "null" : shown)}");
}
