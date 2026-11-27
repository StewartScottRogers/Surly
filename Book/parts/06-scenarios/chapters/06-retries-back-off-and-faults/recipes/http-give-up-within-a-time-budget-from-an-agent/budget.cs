// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp server slow and failing, fetches from it through curl-mcp with a retry
// time budget, times the whole loop and counts what the server did. Run: dotnet run budget.cs -- <case>
//   curl       every try held 1.5 s then 503; curl tool, --retry 10 --retry-max-time 5 (the default)
//   no-budget  the same server; curl tool, --retry 3 and no budget
//   hang       every request held 5 s; curl tool, -m 1 --retry 10 --retry-max-time 3
//   transfer   every request held 5 s; transfer with timeoutSeconds 1, the agent's own 3 s budget
// A second argument, such as "retries", prints only the lines that start with it.
#:package ModelContextProtocol.Core@2.4.0

using System.Text;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "curl";

// A second argument, a label, prints only that label's lines, so each case checks one claim.
if (args.Length > 1)
{
    Console.SetOut(new LineFilter(Console.Out, args[1]));
}
bool hanging = which is "hang" or "transfer";

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

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["report.txt"] = "report ready\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "report.txt";

// The first transfer in curl-mcp pays a one-time set-up: make it, untimed, before any rule.
await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url, ["timeoutSeconds"] = 30 });

// Now the faults: hold each connection 1.5 s and answer every request 503, or hold every request 5 s.
string[] faults = hanging ? ["delay;stage=command;ms=5000"] : ["delay;ms=1500", "error;scheme=http;reply=503"];
foreach (string fault in faults)
{
    await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = server, ["fault"] = fault });
}

var clock = Stopwatch.StartNew();
int tries;
if (which == "transfer")
{
    // transfer makes one attempt per call; timeoutSeconds bounds it. The agent keeps the budget:
    // after a failed call it stops once 3 s have passed, else waits 1 s and calls again.
    int exitCode = 0;
    for (tries = 1; ; tries++)
    {
        CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = url, ["timeoutSeconds"] = 1 });
        exitCode = Answer(fetched).GetProperty("failure").GetProperty("exitCode").GetInt32();
        if (tries == 11 || clock.Elapsed >= TimeSpan.FromSeconds(3))
        {
            break;
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    Console.WriteLine($"transfer: exit {exitCode} after {tries - 1} retries");
}
else
{
    // The curl tool runs a curl command line in-process: curl keeps the budget itself.
    string[] options = which switch
    {
        "no-budget" => ["-f", "--retry", "3", "--retry-delay", "1"],
        "hang" => ["-m", "1", "--retry", "10", "--retry-delay", "1", "--retry-max-time", "3"],
        _ => ["-f", "--retry", "10", "--retry-delay", "1", "--retry-max-time", "5"],
    };
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-w", "%{http_code} %{num_retries}" }.Concat(options).Append(url).ToArray(),
    });
    JsonElement answer = Answer(ran);
    string written = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    Console.WriteLine($"curl: exit {answer.GetProperty("exitCode")}, {written}");
    tries = int.Parse(written.Split(' ')[1]) + 1;
}

double seconds = clock.Elapsed.TotalSeconds;
(double min, double max) = which switch { "no-budget" => (9.0, 40.0), "curl" => (5.0, 15.0), _ => (3.0, 12.0) };
Console.WriteLine($"took {min} to {max} s: {(seconds >= min && seconds < max ? "yes" : $"no, {seconds:0.0} s")}");

// What the server saw: each rule acts once per try.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
});
var byRule = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().GroupBy(e => e.GetProperty("rule_id").GetString()).ToList();
Console.WriteLine($"fault_applied: {string.Join(" ", byRule.Select(g => g.Key))}, once per try: {(byRule.All(g => g.Count() == tries) ? "yes" : "no")}");
return 0;

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
