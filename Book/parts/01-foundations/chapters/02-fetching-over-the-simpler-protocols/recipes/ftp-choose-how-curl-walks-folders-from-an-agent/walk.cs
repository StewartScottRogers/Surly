// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent fetches files three folders down with each --ftp-method through curl-mcp, and reads from
// surl-mcp which path commands each one sent, over the official MCP SDK.
// Run: dotnet run walk.cs [-- default|verbose|locked|missing]
//   (no argument) one file with each of multicwd, singlecwd and nocwd
//   default       a fetch with no --ftp-method
//   verbose       a singlecwd fetch with -v, to read the CWD from the client's side
//   locked        a file under a folder whose CWD the server refuses, with each method
//   missing       a missing file, and a missing folder with multicwd and nocwd
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "methods";

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

// surl-mcp: an FTP server with four files, and a fault rule, in surl's own words, that answers 550
// to every CWD under /locked.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous", "--fault", "error;scheme=ftp;command=CWD;path=/locked;reply=550 CWD is not allowed here" },
    ["files"] = new Dictionary<string, string>
    {
        ["a/b/c/one.txt"] = "one\n",
        ["d/e/f/two.txt"] = "two\n",
        ["g/h/i/three.txt"] = "three\n",
        ["locked/in/there.txt"] = "there\n",
    },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

int lastExchange = 0;
string lastRecord = "";
switch (which)
{
    case "methods":
        await Fetch("multicwd", ["--ftp-method", "multicwd"], "a/b/c/one.txt");
        await Fetch("singlecwd", ["--ftp-method", "singlecwd"], "d/e/f/two.txt");
        await Fetch("nocwd", ["--ftp-method", "nocwd"], "g/h/i/three.txt");
        break;
    case "default":
        await Fetch("default", [], "a/b/c/one.txt");
        break;
    case "verbose":
        await Fetch("verbose", ["-v", "--ftp-method", "singlecwd"], "d/e/f/two.txt");
        break;
    case "locked":
        await Fetch("multicwd", ["--ftp-method", "multicwd"], "locked/in/there.txt");
        Console.WriteLine(lastRecord.Split("\r\n").First(line => line.StartsWith("550")));
        await Fetch("singlecwd", ["--ftp-method", "singlecwd"], "locked/in/there.txt");
        await Fetch("nocwd", ["--ftp-method", "nocwd"], "locked/in/there.txt");
        break;
    case "missing":
        await Fetch("missing file", ["--ftp-method", "multicwd"], "a/b/c/nope.txt");
        await Fetch("missing folder, multicwd", ["--ftp-method", "multicwd"], "a/nope/x.txt");
        await Fetch("missing folder, nocwd", ["--ftp-method", "nocwd"], "a/nope/x.txt");
        break;
}

return 0;

// One curl-mcp call, then the path commands the newest exchange of the server holds.
async Task Fetch(string label, string[] options, string path)
{
    // -s keeps curl's progress meter out of the answer.
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = options.Prepend("-s").Append(url + path).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;

    JsonElement exchange = (await RecordedAsync(EndsWithReply, lastExchange)).GetProperty("exchanges").EnumerateArray().Last();
    lastExchange = exchange.GetProperty("exchange_id").GetInt32();
    var chunks = exchange.GetProperty("chunks").EnumerateArray().ToList();
    string commands = string.Join(", ", chunks
        .Where(chunk => chunk.GetProperty("direction").GetString() == "received")
        .Select(chunk => chunk.GetProperty("data").GetString()!.TrimEnd('\r', '\n'))
        .Where(line => line.StartsWith("CWD ") || line.StartsWith("SIZE ") || line.StartsWith("RETR ")));
    lastRecord = string.Concat(chunks
        .Where(chunk => chunk.GetProperty("direction").GetString() == "sent")
        .Select(chunk => chunk.GetProperty("data").GetString()));

    if (which == "verbose")
    {
        string stderr = answer.GetProperty("standardError").GetProperty("content").GetString()!;
        Console.WriteLine(stderr.Split('\n').Select(line => line.TrimEnd('\r')).First(line => line.StartsWith("> CWD", StringComparison.Ordinal)));
    }

    Console.WriteLine($"{label}: {commands} -> exit {answer.GetProperty("exitCode")}, {answer.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd()}");
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
