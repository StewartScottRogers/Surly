// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent seeds one Redis key name into two databases through surl-mcp, reads each back with surl-mcp and curl-mcp,
// over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   apart      greeting in databases 1 and 2, read from both, and from database 0 (the default)
//   delete     DEL in one database leaves the other
//   list       list_keys and DBSIZE for each database
//   range      database 16
//   no-login   a client with no login
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "apart";

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

if (which == "range")
{
    await SetKey("greeting", "x", 16);
    await Curl("-u", "alice:s3cret", redis + "16/GET/greeting");
    return 0;
}

if (which == "no-login")
{
    await Curl(redis + "1/GET/greeting");
    return 0;
}

// The seed: the same key name in two databases, each with its own value, with no client and no login.
await SetKey("greeting", "from-app-one", 1);
await SetKey("greeting", "from-app-two", 2);

switch (which)
{
    case "delete":
        // DEL runs in database 1 only, and answers how many keys it removed.
        await Curl("-u", "alice:s3cret", redis + "1/DEL/greeting");
        await ReadKey("greeting", 1);
        await ReadKey("greeting", 2);
        break;
    case "list":
        await SetKey("other", "x", 1);
        foreach (int database in new[] { 0, 1, 2 })
        {
            await ListKeys(database);
            await Curl("-u", "alice:s3cret", redis + $"{database}/DBSIZE");
        }

        break;
    default:
        foreach (int database in new[] { 1, 2, 0 })
        {
            await ReadKey("greeting", database);
            // The database number is the first segment of the URL path, then the command and its arguments.
            await Curl("-u", "alice:s3cret", redis + $"{database}/GET/greeting");
        }

        break;
}

return 0;

// set_key takes the database, 0 to 15; a value outside that never reaches surl, and the host answers an invalid-params error.
async Task SetKey(string key, string value, int database)
{
    var arguments = new Dictionary<string, object?>
    {
        ["server_id"] = server, ["key"] = key, ["value"] = value, ["database"] = database,
    };
    string label = $"set_key {key} in database {database}";
    try
    {
        CallToolResult result = await surl.CallToolAsync("set_key", arguments);
        Console.WriteLine(result.IsError == true
            ? $"{label}: tool error: {result.Content.OfType<TextContentBlock>().Single().Text}"
            : $"{label}: {result.StructuredContent!.Value.GetProperty("byte_count")} bytes");
    }
    catch (McpProtocolException refused)
    {
        Console.WriteLine($"{label}: refused: {refused.Message}");
    }
}

// read_key answers the value of one database; a key missing there is a tool error, not an empty value.
async Task ReadKey(string key, int database)
{
    CallToolResult result = await surl.CallToolAsync("read_key", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["key"] = key, ["database"] = database,
    });
    Console.WriteLine(result.IsError == true
        ? $"read_key database {database}: error: {result.Content.OfType<TextContentBlock>().Single().Text}"
        : $"read_key database {database}: {result.StructuredContent!.Value.GetProperty("text")}");
}

// list_keys answers the keys of one database, in order.
async Task ListKeys(int database)
{
    CallToolResult result = await surl.CallToolAsync("list_keys", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["database"] = database,
    });
    IEnumerable<string> keys = result.StructuredContent!.Value.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("key").GetString()!);
    Console.Write($"list_keys database {database}: [{string.Join(", ", keys)}]; ");
}

// The curl tool runs curl's own arguments, the URL path being the Redis command; -s keeps the progress meter out.
async Task Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S" }.Concat(arguments).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd();
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    string command = arguments[^1].Split('/', 4)[^1];
    Console.WriteLine($"curl {command}: exit {answer.GetProperty("exitCode")}, {(output == "" ? "null" : output)}{(error == "" ? "" : $" ({error})")}");
}
