// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds an app's settings and a short-lived cached copy into Redis, and its content into HTTP, through surl-mcp,
// then covers the cache-miss path with curl-mcp, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   expire         seed, read, let the cached copy expire, read again, ask the origin (the default)
//   ttl            the seeded keys' time to live, which of them can go
//   miss-now       force the miss with DEL and refill the copy, with no waiting
//   origin         the origin's statuses
//   no-plaintext   log in without --allow-plaintext-auth
#:package ModelContextProtocol.Core@*

using System.Text.Json;
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

// Redis and HTTP side by side, one account for both, and the origin's file in memory.
var accounts = which == "no-plaintext" ? new[] { "--user", "alice:s3cret" } : new[] { "--user", "alice:s3cret", "--allow-plaintext-auth" };
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "redis://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = accounts,
    ["files"] = new Dictionary<string, string> { ["products.json"] = "{\"items\":[\"tea\",\"coffee\"]}\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string redis = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string http = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

// The seed: the setting has no expiry. set_key needs no client and no login.
await SetKey("settings:currency", "EUR", null);

switch (which)
{
    case "no-plaintext":
        await Curl(redis + "GET/settings:currency");
        return 0;
    case "origin":
        await Status(http + "products.json", login: true);
        await Status(http + "missing.json", login: true);
        await Status(http + "products.json", login: false);
        return 0;
    case "ttl":
        await SetKey("cache:prices", "seeded", 60000);
        foreach (string key in new[] { "settings:currency", "cache:prices", "never-seeded" })
        {
            await ReadKey(key);
            await Curl(redis + $"TTL/{key}");
        }

        return 0;
    case "miss-now":
        await SetKey("cache:orders", "old", 60000);
        await Curl(redis + "DEL/cache:orders");
        await Curl(redis + "GET/cache:orders");
        await SetKey("cache:orders", "refilled", 60000);
        await Curl(redis + "GET/cache:orders");
        return 0;
}

// The agent's first curl call can take seconds to start: it is made first, before any short expiry is running.
await Curl(redis + "GET/settings:currency");

// The cached copy lives four seconds. It is read through both tools at once, while it lives.
await SetKey("cache:products", "cached-copy", 4000);
await ReadKey("cache:products");
await Curl(redis + "GET/cache:products");

// Wait one second past its life, and read again. A slow machine only waits longer.
await Task.Delay(5000);
await Curl(redis + "GET/settings:currency");
await ReadKey("cache:products");
await Curl(redis + "GET/cache:products");
await Curl(http + "products.json");
return 0;

// set_key writes as a SET would, with no client and no login; expire_ms is the key's life in milliseconds.
async Task SetKey(string key, string value, long? expireMs)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = server, ["key"] = key, ["value"] = value };
    if (expireMs is not null)
    {
        arguments["expire_ms"] = expireMs;
    }

    CallToolResult result = await surl.CallToolAsync("set_key", arguments);
    Console.WriteLine($"set_key {key}, expire_ms {(expireMs is null ? "none" : expireMs)}: {result.StructuredContent!.Value.GetProperty("byte_count")} bytes");
}

// read_key answers the value and expires_at, a time while the key has a limit; a key that is gone is a tool error.
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
    string expires = value.TryGetProperty("expires_at", out JsonElement at) && at.ValueKind != JsonValueKind.Null ? "a time" : "none";
    Console.WriteLine($"read_key {key}: {value.GetProperty("text")}, expires_at {expires}");
}

// The curl tool runs curl's own arguments, as alice; -s keeps the progress meter out and -S puts an error message back.
async Task<JsonElement> Run(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", "alice:s3cret" }.Concat(arguments).ToArray(),
    });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}

// The answer of a request: its exit code and body, with the line feed off and an empty body shown as null.
async Task Curl(string url)
{
    JsonElement answer = await Run(url);
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd();
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    string shown = url.Contains("/TTL/") && int.TryParse(output, out int seconds) && seconds > 0 ? "positive" : output;
    Console.WriteLine($"curl {url[url.IndexOf('/', 8)..].TrimStart('/')}: exit {answer.GetProperty("exitCode")}, {(shown == "" ? "null" : shown)}{(error == "" ? "" : $" ({error})")}");
}

// The status line of an HTTP request, from curl -i; a 404 or a 401 is an answer, not a failure.
async Task Status(string url, bool login)
{
    JsonElement answer = login ? await Run("-i", url) : await Curl2("-i", url);
    string statusLine = answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Split('\n')[0].TrimEnd();
    Console.WriteLine($"{url[(url.LastIndexOf('/') + 1)..]} {(login ? "with a login" : "with no login")}: {statusLine}");
}

// The same call with no login: curl-mcp's args are exactly what is given.
async Task<JsonElement> Curl2(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(arguments).ToArray(),
    });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}
