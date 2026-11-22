// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent lists what a running surl-mcp server holds with list_files, then fetches the files it found.
// Run: dotnet run list.cs -- <case>
//   list         list / and fetch each file in it (the default)
//   folder       list /docs
//   page         list / two entries at a time
//   missing      list a folder that is not there
//   not-rooted   list a path without its leading /
#:package ModelContextProtocol.Core@*

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

// surl-mcp: a server holding a few files, one of them binary, one a dot file, two in folders.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string>
    {
        ["hello.txt"] = "hello from surl\n",
        ["notes.txt"] = "two\nlines\n",
        [".env"] = "SECRET=1\n",
        ["docs/guide.md"] = "# Guide\n",
        ["docs/archive/old.md"] = "# Old\n",
    },
    ["files_base64"] = new Dictionary<string, string> { ["logo.png"] = "iVBORw0KGgo=" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()!;

CallToolResult result;
switch (which)
{
    case "page":
        // Two at a time: after_name is the last name seen, has_more says whether to ask again.
        string? after = null;
        do
        {
            var page = new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/", ["limit"] = 2 };
            if (after is not null)
            {
                page["after_name"] = after;
            }

            result = await surl.CallToolAsync("list_files", page);
            JsonElement listing = result.StructuredContent!.Value;
            after = Print(listing).LastOrDefault();
            Console.WriteLine($"has_more: {listing.GetProperty("has_more").GetRawText()}");
            if (!listing.GetProperty("has_more").GetBoolean())
            {
                after = null;
            }
        }
        while (after is not null);
        break;

    case "missing" or "not-rooted":
        result = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = which == "missing" ? "/nope" : "docs" });
        Console.WriteLine(result.Content.OfType<TextContentBlock>().Single().Text);
        break;

    case "folder":
        result = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/docs" });
        Print(result.StructuredContent!.Value);
        break;

    default:
        result = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/" });
        JsonElement entries = result.StructuredContent!.Value;
        Print(entries);

        // Decide from the listing: fetch each text file, skip folders and anything else.
        foreach (JsonElement entry in entries.GetProperty("entries").EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString()!;
            if (entry.GetProperty("kind").GetString() == "file" && name.EndsWith(".txt"))
            {
                CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + name });
                JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
                Console.WriteLine($"fetched {name}: {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("totalBytes")} bytes");
            }
        }

        break;
}

Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;

// One line per entry: its name, its kind, and a file's size. Returns the names.
static List<string> Print(JsonElement listing)
{
    var names = new List<string>();
    foreach (JsonElement entry in listing.GetProperty("entries").EnumerateArray())
    {
        string name = entry.GetProperty("name").GetString()!;
        string size = entry.TryGetProperty("byte_count", out JsonElement count) ? $" {count} bytes" : "";
        Console.WriteLine($"{name} {entry.GetProperty("kind")}{size}");
        names.Add(name);
    }

    return names;
}
