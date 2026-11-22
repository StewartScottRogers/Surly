// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent resumes a partial FTP download with curl-mcp and reads from surl-mcp the REST the server saw,
// over the official MCP SDK.
// Run: dotnet run resume.cs [-- fresh|restart|offset|wrong|verbose]
//   (no argument) a partial copy of 65,536 bytes is resumed with -C -
//   fresh         no local file at all
//   restart       the same partial copy, fetched again without -C
//   offset        -C 100000, whatever is on disk
//   wrong         a partial copy of 65,536 other bytes
//   verbose       the resume with -v, to read the REST from the client's side
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "resume";

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

// The image: 200,000 characters of numbered lines, text so that it travels as a string.
string image = string.Concat(Enumerable.Range(0, 20_000).Select(i => $"line {i:D5}\n"))[..200_000];

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous" },
    ["files"] = new Dictionary<string, string> { ["update.iso"] = image },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// What a download cut off at 65,536 bytes leaves in the agent's root, or what this run starts from.
const string Local = "update.iso";
File.Delete(Local);
if (which is not ("fresh" or "offset"))
{
    File.WriteAllText(Local, which == "wrong" ? new string('0', 65_536) : image[..65_536]);
}

string[] options = which switch
{
    "restart" => [],
    "offset" => ["-C", "100000"],
    "verbose" => ["-v", "-C", "-"],
    _ => ["-C", "-"],
};
(int code, string output, string stderr) = await Curl(options.Prepend("-s").Concat(["-o", Local, "-w", "%{size_download}", url + "update.iso"]).ToArray());

// The commands the server received and the 350 it sent, from the one exchange.
var chunks = (await RecordedAsync(EndsWithReply)).GetProperty("exchanges").EnumerateArray().Last().GetProperty("chunks").EnumerateArray().ToList();
string[] received = chunks.Where(c => c.GetProperty("direction").GetString() == "received")
    .Select(c => c.GetProperty("data").GetString()!.TrimEnd('\r', '\n')).ToArray();
string sent = string.Concat(chunks.Where(c => c.GetProperty("direction").GetString() == "sent").Select(c => c.GetProperty("data").GetString()));

string got = File.ReadAllText(Local);
string expected = which == "offset" ? image[100_000..] : image;
bool same = got == expected;
Console.WriteLine($"{which}: exit {code}, {output} bytes fetched");
Console.WriteLine($"{Local}: {got.Length} bytes, {(same ? (which == "offset" ? "identical to the last 100000 bytes of the server's" : "identical to the server's") : "DIFFERENT from the server's")}");
Console.WriteLine($"server received: {string.Join(", ", received.Where(l => l.StartsWith("REST ") || l.StartsWith("RETR ")))}");
Console.WriteLine($"server answered: {sent.Split("\r\n").FirstOrDefault(l => l.StartsWith("350")) ?? "no 350"}");
if (which == "verbose")
{
    Console.WriteLine(stderr.Split('\n').Select(line => line.TrimEnd('\r')).First(line => line.StartsWith("> REST", StringComparison.Ordinal)));
}

return same ? 0 : 1;

// One curl-mcp call: the exit code, what curl wrote to standard output and to standard error.
async Task<(int Code, string Out, string Err)> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(),
        answer.GetProperty("standardOutput").GetProperty("content").GetString()!,
        answer.GetProperty("standardError").GetProperty("content").GetString()!);
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
