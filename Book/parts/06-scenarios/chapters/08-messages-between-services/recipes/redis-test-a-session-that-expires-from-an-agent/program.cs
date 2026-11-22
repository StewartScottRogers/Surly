// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a session that expires: surl-mcp holds the Redis store and the app's page, set_key seeds a
// short-lived session, the curl tool of curl-mcp makes the app's calls, and the agent asks again until the app
// says the user is logged out.
// Run: dotnet run program.cs -- <scenario>
//   solution   a session that lives six seconds, the app before and after, and the store afterwards
//   lifetimes  a session with a minute to live, one with no expiry, and one never seeded
//   strict     a server started without --allow-plaintext-auth
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "solution";

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

// One server: a Redis store for the sessions and an HTTP listener for the app's page, one account for both.
var arguments = new List<string> { "--user", "app:s3cret" };
if (scenario != "strict")
{
    arguments.Add("--allow-plaintext-auth");
}

JsonElement started = (await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "redis://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["account.txt"] = "your orders and settings\n" },
})).StructuredContent!.Value;
string id = started.GetProperty("server_id").GetString()!;
string redis = started.GetProperty("bound_urls")[0].GetString()!, http = started.GetProperty("bound_urls")[1].GetString()!;

if (scenario == "strict")
{
    (int code, _, string error) = await Curl("-s", "-S", "-u", "app:s3cret", $"{redis}GET/session:abc");
    Console.WriteLine($"app: exit {code}, {error}");
    return 0;
}

// A first call to the curl tool can be slow; make one before the clock starts.
await App("warm-up");

if (scenario == "solution")
{
    await Surl("set_key", new() { ["key"] = "session:abc", ["value"] = "alice", ["expire_ms"] = 6000 });
    Console.WriteLine($"at once: {await App("abc")}");

    // Ask again once a second, at most fifteen times, until the app says logged out.
    string answer = "";
    for (int attempt = 0; attempt < 15 && !answer.StartsWith("logged out", StringComparison.Ordinal); attempt++)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        answer = await App("abc");
    }

    Console.WriteLine($"later: {answer}");
    Console.WriteLine($"store: {await Stored("session:abc")}");
}
else
{
    await Surl("set_key", new() { ["key"] = "session:def", ["value"] = "bob", ["expire_ms"] = 60000 });
    Console.WriteLine($"with a minute: {await Stored("session:def")}");
    await Surl("set_key", new() { ["key"] = "session:ghi", ["value"] = "carol" });
    Console.WriteLine($"with no expiry: {await Stored("session:ghi")}");
    Console.WriteLine($"never seeded: {await App("nobody")}");
}

return 0;

// The app: looks the session up with GET over Redis, which writes the value and a line feed, or a lone line feed
// for a missing or expired key. With a user, it shows the account page it fetches over HTTP.
async Task<string> App(string session)
{
    (_, string user, _) = await Curl("-s", "-u", "app:s3cret", $"{redis}GET/session:{session}");
    if (user.Trim().Length == 0)
    {
        return "logged out: sign in again";
    }

    (_, string page, _) = await Curl("-s", "-u", "app:s3cret", $"{http}account.txt");
    return $"logged in as {user.Trim()}: {page.Trim()}";
}

// The store, read from the server's side: read_key answers the value and expires_at, a key with no expiry has no
// expires_at, and a missing or expired key is a tool error.
async Task<string> Stored(string key)
{
    CallToolResult read = await surl.CallToolAsync("read_key", new Dictionary<string, object?> { ["server_id"] = id, ["key"] = key });
    if (read.IsError == true)
    {
        return $"tool error, {read.Content.OfType<TextContentBlock>().First().Text}";
    }

    JsonElement contents = read.StructuredContent!.Value;
    if (!contents.TryGetProperty("expires_at", out JsonElement at))
    {
        return $"{contents.GetProperty("text").GetString()}, no expiry";
    }

    double left = (DateTimeOffset.Parse(at.GetString()!) - DateTimeOffset.UtcNow).TotalSeconds;
    return $"{contents.GetProperty("text").GetString()}, {(left is > 0 and <= 60 ? "expires within 60 s" : $"expires in {left} s")}";
}

async Task<JsonElement> Surl(string tool, Dictionary<string, object?> toolArguments)
{
    toolArguments["server_id"] = id;
    CallToolResult result = await surl.CallToolAsync(tool, toolArguments);
    return result.StructuredContent?.Clone() ?? default;
}

// curl-mcp's curl tool runs curl with the arguments given; its answer is text, as JSON.
async Task<(int, string, string)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(), answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim());
}
