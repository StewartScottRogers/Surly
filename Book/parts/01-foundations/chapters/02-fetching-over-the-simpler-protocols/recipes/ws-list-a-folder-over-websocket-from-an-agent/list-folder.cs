// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a folder over WebSocket with surl-mcp and reads its listing with curl-mcp, over the
// official MCP SDK.
// Run: dotnet run list-folder.cs [-- <case>]
//   list       the curl tool reads the root's listing, and get_exchanges shows the upgrade (the default)
//   folders    a sub-folder with and without the closing slash, and a file
//   transfer   the transfer tool reads the root
//   off        a server started without --list-directories
//   missing    a path that is not there, and a dot file the server holds
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

// surl-mcp: a WebSocket server on a free port with a folder in memory. Listings are off unless the
// run asks for them, which passes surl's option of that name as surl's own words.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ws://127.0.0.1:0" },
    ["arguments"] = which == "off" ? Array.Empty<string>() : new[] { "--list-directories" },
    ["files"] = new Dictionary<string, string>
    {
        ["hello.txt"] = "hello from surl\n",
        ["notes/today.txt"] = "nothing happened\n",
        [".secret"] = "not for you\n",
    },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string root = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "list":
        await Show("/", ["-s", root]);

        // What the server saw: the upgrade it accepted, and the one text message it sent.
        JsonElement chunks = (await RecordedAsync(answer => answer.GetProperty("exchanges").GetArrayLength() > 0)).GetProperty("exchanges")[0].GetProperty("chunks");
        foreach (JsonElement chunk in chunks.EnumerateArray())
        {
            // The upgrade is text. A frame is binary, so base64: its first byte says what it is, 0x81 a text
            // frame and 0x88 a close, and its second the length of the payload.
            string data = chunk.GetProperty("data").GetString()!;
            string what = chunk.GetProperty("encoding").GetString() == "text"
                ? data.Split('\r')[0]
                : Convert.FromBase64String(data) switch
                {
                    [0x81, var length, ..] => $"text frame, {length} bytes",
                    [0x88, ..] => "close frame",
                    _ => "other frame",
                };
            Console.WriteLine($"{chunk.GetProperty("direction").GetString()}: {what}");
        }

        break;
    case "folders":
        await Show("/notes/", ["-s", root + "notes/"]);
        await Show("/notes", ["-s", root + "notes"]);
        await Show("/hello.txt", ["-s", root + "hello.txt"]);
        break;
    case "transfer":
        CallToolResult transferred = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = root });
        JsonElement answer = JsonDocument.Parse(transferred.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        Console.WriteLine($"statusCode: {answer.GetProperty("statusCode")}");
        Console.WriteLine($"failure: {answer.GetProperty("failure").ValueKind}");
        Console.Write(answer.GetProperty("body").GetProperty("content").GetString());
        Console.WriteLine($"tool error: {(transferred.IsError == true ? "yes" : "no")}");
        break;
    case "off":
        await Show("/", ["-s", root]);

        // The server's record says what curl's exit code cannot: it answered the upgrade 404.
        foreach (JsonElement chunk in (await RecordedAsync(EndsWithReply)).GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray())
        {
            Console.WriteLine($"{chunk.GetProperty("direction").GetString()}: {chunk.GetProperty("data").GetString()!.Split('\r')[0]}");
        }

        break;
    case "missing":
        await Show("/nowhere/", ["-s", root + "nowhere/"]);
        await Show("/.secret", ["-s", root + ".secret"]);
        break;
}

return 0;

// One curl-mcp call: curl with these arguments. The answer is text, JSON, and a failed run is still an answer.
async Task Show(string label, string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement ran = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{label} exit: {ran.GetProperty("exitCode")}");
    if (ran.GetProperty("exitCode").GetInt32() == 0)
    {
        Console.Write(ran.GetProperty("standardOutput").GetProperty("content").GetString());
    }
    else
    {
        Console.WriteLine(ran.GetProperty("failure").GetProperty("text").GetString()!.Replace("\n", " / "));
    }
}

// get_exchanges, asked again until the record holds what ready looks for and two answers 100 ms apart
// agree: the server's record can trail curl by a moment. At most 10 s, then the last answer.
async Task<JsonElement> RecordedAsync(Func<JsonElement, bool> ready, int after = 0)
{
    JsonElement last = default;
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId, ["after_exchange_id"] = after });
        JsonElement answer = result.StructuredContent!.Value.Clone();
        if (ready(answer) && last.ValueKind != JsonValueKind.Undefined && answer.GetRawText() == last.GetRawText())
        {
            return answer;
        }

        last = answer;
    }

    return last;
}

// True once the newest exchange ends with something the server sent, as every finished exchange here does.
static bool EndsWithReply(JsonElement answer) =>
    answer.GetProperty("exchanges").EnumerateArray().LastOrDefault() is { ValueKind: JsonValueKind.Object } newest
    && newest.GetProperty("chunks").EnumerateArray().LastOrDefault() is { ValueKind: JsonValueKind.Object } final
    && final.GetProperty("direction").GetString() == "sent";
