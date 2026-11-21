// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent lets "the app" write keys into a surl Redis store, then lists what it left, reads each value and
// expiry, flags the keys with no expiry, and asks the server what it recorded, over the official MCP SDK.
// Run: dotnet run program.cs -- <case>
//   inspect         the app runs; list its keys, read them, flag those with no expiry (the default)
//   pages           list the app's keys two at a time
//   recorded        what the server recorded of the app's writes
//   wrong-password  the app runs with the wrong password and leaves nothing
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "inspect";

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

// The store: a Redis listener on a free port and one account, app, whose password may cross it in clear.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "redis://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "app:pw", "--allow-plaintext-auth" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string redis = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The app: one curl run per write, as the account app. Two keys carry an hour's expiry (EX 3600), two forgot it,
// and one is not the app's namespace. With the wrong password the app writes nothing.
string password = which == "wrong-password" ? "wrong" : "pw";
foreach (string write in new[]
{
    "SET/app:session:1/s1/EX/3600", "SET/app:session:2/s2/EX/3600", "SET/app:cache:home/page", "SET/app:config/v1", "SET/other:x/1",
})
{
    CallToolResult run = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "-u", $"app:{password}", redis + write },
    });
    JsonElement answer = JsonDocument.Parse(run.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    if (which == "wrong-password")
    {
        string error = answer.GetProperty("standardError").GetProperty("content").GetString()!;
        Console.WriteLine($"app: exit {answer.GetProperty("exitCode")}, {error[..error.IndexOf("WRONGPASS", StringComparison.Ordinal)]}WRONGPASS");
        break;
    }
}

switch (which)
{
    case "pages":
        // list_keys cuts a page at limit and says has_more; after_key starts the next page after the key given.
        JsonElement page = await ListKeys(new() { ["pattern"] = "app:*", ["limit"] = 2 });
        string last = page.GetProperty("keys").EnumerateArray().Last().GetProperty("key").GetString()!;
        Console.WriteLine($"page 1: {Names(page)}, has_more {page.GetProperty("has_more")}");
        page = await ListKeys(new() { ["pattern"] = "app:*", ["limit"] = 2, ["after_key"] = last });
        Console.WriteLine($"page 2: {Names(page)}, has_more {page.GetProperty("has_more")}");
        break;

    case "recorded":
        // get_events keeps what the server saw: each login it judged and each change to the keyspace.
        CallToolResult changes = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["kinds"] = new[] { "store_changed" }, ["store"] = "keyspace",
        });
        foreach (JsonElement change in changes.StructuredContent!.Value.GetProperty("events").EnumerateArray())
        {
            Console.WriteLine($"{change.GetProperty("change")}: {change.GetProperty("name")}");
        }

        CallToolResult logins = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["kinds"] = new[] { "login_judged" }, ["account"] = "app",
        });
        int accepted = logins.StructuredContent!.Value.GetProperty("events").EnumerateArray().Count(e => e.GetProperty("is_accepted").GetBoolean());
        Console.WriteLine($"logins as app: {accepted} accepted");
        break;

    case "wrong-password":
        // The refused logins are in the record, and the store holds nothing of the app's.
        CallToolResult refused = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["kinds"] = new[] { "login_judged" },
        });
        JsonElement.ArrayEnumerator judged = refused.StructuredContent!.Value.GetProperty("events").EnumerateArray();
        Console.WriteLine($"login judged: account {judged.First().GetProperty("account")}, accepted {judged.First().GetProperty("is_accepted")}");
        Console.WriteLine($"app's keys: [{Names(await ListKeys(new() { ["pattern"] = "app:*" }))}]");
        CallToolResult missing = await surl.CallToolAsync("read_key", new Dictionary<string, object?> { ["server_id"] = server, ["key"] = "app:config" });
        Console.WriteLine($"read_key app:config: {(missing.IsError == true ? "error: " + missing.Content.OfType<TextContentBlock>().Single().Text : "found")}");
        break;

    default:
        // list_keys answers each key with its type and size, and expires_at only for a key that has one.
        JsonElement list = await ListKeys(new() { ["pattern"] = "app:*" });
        int unlimited = 0;
        foreach (JsonElement key in list.GetProperty("keys").EnumerateArray())
        {
            string name = key.GetProperty("key").GetString()!;
            CallToolResult read = await surl.CallToolAsync("read_key", new Dictionary<string, object?> { ["server_id"] = server, ["key"] = name });
            string value = read.StructuredContent!.Value.GetProperty("text").GetString()!;
            if (key.TryGetProperty("expires_at", out JsonElement at))
            {
                Console.WriteLine($"{name} = {value}, expires in about {Math.Round((DateTimeOffset.Parse(at.GetString()!) - DateTimeOffset.UtcNow).TotalMinutes)} minutes");
            }
            else
            {
                unlimited++;
                Console.WriteLine($"{name} = {value}, NO EXPIRY");
            }
        }

        Console.WriteLine($"{list.GetProperty("keys").GetArrayLength()} keys, {unlimited} without an expiry");
        break;
}

return 0;

async Task<JsonElement> ListKeys(Dictionary<string, object?> arguments)
{
    arguments["server_id"] = server;
    return (await surl.CallToolAsync("list_keys", arguments)).StructuredContent!.Value;
}

static string Names(JsonElement page) =>
    string.Join(", ", page.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("key").GetString()));
