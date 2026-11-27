// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent pages through the Redis keys a cache wrote that match a pattern, and reads one, with surl-mcp's
// list_keys and read_key, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   list      the cache wrote seven keys; the agent lists those that match cache:* (the default)
//   paging    the agent lists cache:page:* two keys at a time
//   patterns  other patterns: one with a question mark, one with brackets, and one that matches nothing
//   read      the agent reads a key with an expiry and a key without
//   no-key    the agent reads a key that is not there
//   closed    the server keeps its default and refuses the cache's password
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "list";

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

// A Redis server with one account, whose password may cross the plain listener unless the case says no.
var arguments = new List<string> { "--user", "app:s3cret" };
if (which != "closed")
{
    arguments.Add("--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "redis://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string redis = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The cache under test writes its keys: one curl run with several URLs, each a SET. The last key has a life of ten minutes.
string[] writes =
[
    "SET/cache:page:1/home", "SET/cache:page:2/about", "SET/cache:page:3/contact", "SET/cache:page:4/pricing",
    "SET/cache:user:ann/Ann", "SET/session:abc/ann", "SET/cache:page:5/blog/EX/600",
];
(int exit, _, string error) = await Curl(["-u", "app:s3cret", .. writes.Select(w => redis + w)]);
if (exit != 0)
{
    Console.WriteLine($"cache could not write: exit {exit}, WRONGPASS in the message: {error.Contains("WRONGPASS")}");
}
else
{
    await WaitForWrite("0/cache:page:5");
}

switch (which)
{
    case "paging":
        string? after = null;
        while (true)
        {
            JsonElement page = await List("cache:page:*", after, 2);
            JsonElement[] keys = page.GetProperty("keys").EnumerateArray().ToArray();
            Console.WriteLine($"cache:page:* after {after ?? "(start)"}: {string.Join(", ", keys.Select(k => k.GetProperty("key").GetString()))} (has_more {page.GetProperty("has_more")})");
            if (!page.GetProperty("has_more").GetBoolean())
            {
                break;
            }

            after = keys[^1].GetProperty("key").GetString();
        }

        break;
    case "patterns":
        await Show("cache:page:?");
        await Show("cache:page:[1-2]");
        await Show("nothing:*");
        break;
    case "read":
        await Read("cache:page:2");
        await Read("cache:page:5");
        break;
    case "no-key":
        await Read("cache:page:9");
        break;
    case "closed":
        await Show("*");
        break;
    default:
        await Show("cache:*");
        await Read("cache:page:3");
        break;
}

return 0;

// list_keys answers the keys that match a Redis glob, in order, a page at a time.
async Task<JsonElement> List(string pattern, string? afterKey, int limit)
{
    var arguments = new Dictionary<string, object?> { ["server_id"] = server, ["pattern"] = pattern, ["limit"] = limit };
    if (afterKey is not null)
    {
        arguments["after_key"] = afterKey;
    }

    CallToolResult result = await surl.CallToolAsync("list_keys", arguments);
    return result.StructuredContent!.Value;
}

async Task Show(string pattern)
{
    JsonElement page = await List(pattern, null, 100);
    string[] keys = page.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("key").GetString()!).ToArray();
    Console.WriteLine($"{pattern}: {keys.Length} keys{(keys.Length == 0 ? "" : ": " + string.Join(", ", keys))} (has_more {page.GetProperty("has_more")})");
}

// read_key answers the type, the size and the value of one key, and when it expires if it does.
async Task Read(string key)
{
    CallToolResult result = await surl.CallToolAsync("read_key", new Dictionary<string, object?> { ["server_id"] = server, ["key"] = key });
    if (result.IsError == true)
    {
        Console.WriteLine($"{key}: tool error, {result.Content.OfType<TextContentBlock>().Single().Text}");
        return;
    }

    JsonElement answer = result.StructuredContent!.Value;
    Console.WriteLine($"{key}: {answer.GetProperty("type")}, {answer.GetProperty("byte_count")} bytes, text {answer.GetProperty("text")}, expires {(answer.TryGetProperty("expires_at", out _) ? "set" : "never")}");
}

// The curl tool runs curl's own arguments; -s keeps the progress meter out and -S puts an error message back.
async Task<(int Exit, string Output, string Error)> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(curlArguments).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(),
        answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd());
}

// The last write is done when the keyspace has recorded it: wait for that event, at most ten seconds.
async Task WaitForWrite(string name)
{
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "store_changed" }, ["store"] = "keyspace", ["name"] = name, ["timeout_ms"] = 10000,
    });
    if (!waited.StructuredContent!.Value.TryGetProperty("event", out _))
    {
        throw new TimeoutException($"the keyspace did not record {name} in 10 s");
    }
}
