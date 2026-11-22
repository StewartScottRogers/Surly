// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent downloads a file over FTP with no login from a server it starts with surl-mcp, first without
// --allow-anonymous and then with it, over the official MCP SDK.
// Run: dotnet run fetch-ftp.cs [-- <case>]
//   refused    a server with no --allow-anonymous: the login is refused (the default)
//   allowed    a server with --allow-anonymous: the file arrives, and the record shows the login
//   any-login  --allow-anonymous and a user name and password nobody configured
//   missing    a file the server does not have
//   dot-file   a file whose name begins with a dot
//   transfer   the transfer tool against the refusing and the allowing server
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "refused";

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

// surl-mcp: an FTP server on a free port with two files in memory. Anonymous logins are refused unless
// the run asks for them, which passes surl's option of that name as surl's own words.
(string serverId, string root) = await Start(which != "refused" && which != "transfer");

switch (which)
{
    case "refused":
        await Show(["-s", root + "hello.txt"]);
        break;
    case "allowed":
        await Show(["-s", root + "hello.txt"]);

        // The record of the one control connection: what the client sent. The conversation is text.
        JsonElement chunks = (await RecordedAsync(EndsWithReply)).GetProperty("exchanges")[0].GetProperty("chunks");
        foreach (JsonElement chunk in chunks.EnumerateArray().Where(c => c.GetProperty("direction").GetString() == "received"))
        {
            Console.Write($"client sent: {chunk.GetProperty("data").GetString()}");
        }

        break;
    case "any-login":
        await Show(["-s", "-u", "nobody:anything", root + "hello.txt"]);
        break;
    case "missing":
        await Show(["-s", root + "missing.txt"]);
        break;
    case "dot-file":
        await Show(["-s", root + ".secret"]);
        break;
    case "transfer":
        // transfer describes the request by fields. Against the refusing server it is a failure, against
        // an allowing one a status-less success.
        await Transfer("refusing", root);
        (_, string open) = await Start(true);
        await Transfer("allowing", open);
        break;
}

return 0;

async Task<(string, string)> Start(bool anonymous)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
        ["arguments"] = anonymous ? new[] { "--allow-anonymous" } : Array.Empty<string>(),
        ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n", [".secret"] = "not for you\n" },
    });
    return (started.StructuredContent!.Value.GetProperty("server_id").GetString()!,
        started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!);
}

// One curl-mcp call: curl with these arguments. The answer is text, JSON, and a failed run is still an answer.
async Task Show(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement ran = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exit: {ran.GetProperty("exitCode")}");
    if (ran.GetProperty("exitCode").GetInt32() == 0)
    {
        Console.Write(ran.GetProperty("standardOutput").GetProperty("content").GetString());
    }
    else
    {
        Console.WriteLine(ran.GetProperty("failure").GetProperty("text").GetString()!.Replace("\n", " / "));
    }
}

async Task Transfer(string label, string url)
{
    CallToolResult result = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url + "hello.txt" });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"{label}: tool error {(result.IsError == true ? "yes" : "no")}");
    if (answer.GetProperty("failure").ValueKind == JsonValueKind.Null)
    {
        // The status is the one of the last reply on the control connection, 226 for a finished transfer.
        Console.WriteLine($"statusCode {answer.GetProperty("statusCode")}");
        Console.WriteLine(answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd());
    }
    else
    {
        JsonElement failure = answer.GetProperty("failure");
        Console.WriteLine($"{failure.GetProperty("kind")}, exit code {failure.GetProperty("exitCode")}");
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
