// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp server fail four paths in four ways, fetches each through curl-mcp
// under a retry policy, and counts what the server saw. Run: dotnet run retry-policy.cs -- <case>
//   curl      the curl tool with -f --retry 2 --retry-delay 1 (the default)
//   all       the same with --retry-all-errors
//   transfer  transfer in a loop, with the agent's own policy: retry 408, 429, 500, 502, 503, 504
// A second argument, such as "retries", prints only the lines that start with it.
#:package ModelContextProtocol.Core@2.4.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "curl";

// A second argument, a label, prints only that label's lines, so each case checks one claim.
if (args.Length > 1)
{
    Console.SetOut(new LineFilter(Console.Out, args[1]));
}

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

// A server with three files in memory and three rules: /report.txt busy (503) for one request,
// /broken.txt always 500, /locked.txt always 403. /missing.txt is not there, a plain 404.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string>
    {
        ["report.txt"] = "report ready\n", ["broken.txt"] = "never served\n", ["locked.txt"] = "never served\n",
    },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string baseUrl = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
foreach (string fault in new[]
{
    "error;scheme=http;reply=503;path=/report.txt;times=1",
    "error;scheme=http;reply=500;path=/broken.txt",
    "error;scheme=http;reply=403;path=/locked.txt",
})
{
    CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = server, ["fault"] = fault });
    Console.Write($"{added.StructuredContent!.Value.GetProperty("fault_id")} ");
}

Console.WriteLine("added");
string[] paths = ["report.txt", "missing.txt", "locked.txt", "broken.txt"];
int[] transient = [408, 429, 500, 502, 503, 504];
foreach (string path in paths)
{
    if (which == "transfer")
    {
        // transfer makes one attempt per call and an error status is an answer, not a tool error:
        // the agent reads statusCode and decides, one second apart, at most two retries.
        int status = 0, retries = 0;
        for (; ; retries++)
        {
            CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = baseUrl + path });
            status = Answer(fetched).GetProperty("statusCode").GetInt32();
            if (status < 400 || retries == 2 || !transient.Contains(status))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Console.WriteLine($"transfer {path}: {status}, retries {retries}");
    }
    else
    {
        // The curl tool runs a curl command line in-process; -w prints the last status and the retries.
        string[] options = ["-s", "-f", "--retry", "2", "--retry-delay", "1", "-o", "-", "-w", "%{http_code} %{num_retries}"];
        CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
        {
            ["args"] = (which == "all" ? options.Append("--retry-all-errors") : options).Append(baseUrl + path).ToArray(),
        });
        JsonElement answer = Answer(ran);
        string written = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
        Console.WriteLine($"curl {path}: exit {answer.GetProperty("exitCode")}, {written.Split('\n')[^1]}");
    }
}

// What the server saw: the requests for each path, from the bytes each client sent, and how
// often each rule answered, from its fault_applied events.
await WaitForExchangesToEnd();
CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
string received = string.Concat(exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray()
    .SelectMany(e => e.GetProperty("chunks").EnumerateArray())
    .Where(c => c.GetProperty("direction").GetString() == "received")
    .Select(c => c.GetProperty("data").GetString()));
Console.WriteLine($"requests: {string.Join(", ", paths.Select(p => $"{p} {received.Split($"GET /{p} HTTP/1.1").Length - 1}"))}");
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
});
var byRule = events.StructuredContent!.Value.GetProperty("events").EnumerateArray()
    .GroupBy(e => e.GetProperty("rule_id").GetString()).Select(g => $"{g.Key} {g.Count()}");
Console.WriteLine($"fault_applied: {string.Join(", ", byRule)}");
return 0;

// The server records an exchange's end a moment after its client is done: ask again until every
// exchange it started has ended, for at most 10 s, before reading what it recorded.
async Task WaitForExchangesToEnd()
{
    for (var waited = Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["kinds"] = new[] { "exchange_started", "exchange_ended" }, ["max_events"] = 500,
        });
        if (events.StructuredContent!.Value.GetProperty("events").EnumerateArray().Sum(e => e.GetProperty("kind").GetString() == "exchange_started" ? 1 : -1) == 0)
        {
            return;
        }
    }
}

// curl-mcp answers in text, as JSON.
static JsonElement Answer(CallToolResult result) =>
    JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;

// Shows only the lines that start with one label, and the indented lines under them.
sealed class LineFilter(TextWriter inner, string label) : TextWriter
{
    private readonly StringBuilder line = new();
    private bool keeping;

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        if (value == '\r')
        {
            return;
        }

        if (value != '\n')
        {
            line.Append(value);
            return;
        }

        string text = line.ToString();
        line.Clear();
        keeping = text.StartsWith(label) || (keeping && text.StartsWith(' '));
        if (keeping)
        {
            inner.Write(text + "\n");
            inner.Flush();
        }
    }
}
