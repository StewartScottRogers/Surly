// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent makes a surl-mcp FTP server refuse the first login on purpose, fetches a file through
// curl-mcp with retries, and reads what the server judged. Run: dotnet run retry-login.cs -- <case>
//   curl        the curl tool with --retry 3 --retry-all-errors (the default)
//   retry-only  the curl tool with --retry 3 alone, then a second call
//   wrong       the curl tool with a wrong password and --retry 2 --retry-all-errors --retry-delay 1
//   slow        a rule that refuses two logins; the curl tool with --retry-delay 2
//   transfer    transfer in a loop, with the agent's own one-second wait
//   strict      no rule, and a server not told to check a password sent in clear
// A second argument, such as "retries", prints only the lines that start with it.
#:package ModelContextProtocol.Core@2.2.0

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

// An FTP server on a free port with one account and one file in memory, then a rule that
// refuses the first login (two for "slow") whatever the password.
string[] serverArguments = which == "strict" ? ["--user", "app:s3cret"] : ["--user", "app:s3cret", "--allow-plaintext-auth"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = serverArguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "hello.txt";
if (which != "strict")
{
    CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["fault"] = which == "slow" ? "refuse-login;times=2" : "refuse-login;times=1",
    });
    Console.WriteLine($"add_fault: {added.StructuredContent!.Value.GetProperty("fault_id")}");
}

if (which == "transfer")
{
    // transfer makes one attempt each call and takes the account in the URL; a refused login is
    // a tool error with exit code 67, so the agent waits a second and calls again.
    string withLogin = url.Replace("ftp://", "ftp://app:s3cret@");
    for (int attempt = 1; attempt <= 4; attempt++)
    {
        CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = withLogin });
        JsonElement answer = Answer(fetched);
        Console.Write($"transfer {attempt}: tool error: {(fetched.IsError == true ? "yes" : "no")}");
        if (fetched.IsError != true)
        {
            Console.Write($", {answer.GetProperty("body").GetProperty("content").GetString()}");
            break;
        }

        Console.WriteLine($", exit code {answer.GetProperty("failure").GetProperty("exitCode")}, waiting 1 s");
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}
else
{
    string[] options = which switch
    {
        "retry-only" => ["-u", "app:s3cret", "--retry", "3"],
        "wrong" => ["-u", "app:wrong", "--retry", "2", "--retry-all-errors", "--retry-delay", "1"],
        "slow" => ["-u", "app:s3cret", "--retry", "3", "--retry-all-errors", "--retry-delay", "2"],
        "strict" => ["-u", "app:s3cret", "--retry", "1", "--retry-all-errors", "--retry-delay", "1"],
        _ => ["-u", "app:s3cret", "--retry", "3", "--retry-all-errors"],
    };
    await RunCurl(options);
    if (which == "retry-only")
    {
        await RunCurl(["-u", "app:s3cret"]);
    }
}

// What the server judged: each login, accepted or refused, and whether the rule refused it.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "login_judged" },
});
IEnumerable<string> logins = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e =>
    e.GetProperty("is_accepted").GetBoolean() ? "accepted" : e.GetProperty("is_refused_by_fault").GetBoolean() ? "refused by the rule" : "refused");
Console.WriteLine($"get_events: {string.Join(", ", logins)}");
CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = server });
foreach (JsonElement rule in faults.StructuredContent!.Value.GetProperty("faults").EnumerateArray())
{
    Console.WriteLine($"list_faults: {rule.GetProperty("fault_id")} remaining {rule.GetProperty("remaining")}");
}

return 0;

// The curl tool runs a curl command line in-process; print its exit code and both streams.
async Task RunCurl(string[] options)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "--no-progress-meter", "-w", "%{exitcode} %{num_retries}\n" }.Concat(options).Append(url).ToArray(),
    });
    JsonElement answer = Answer(ran);
    Console.WriteLine($"curl: exit {answer.GetProperty("exitCode")}, tool error: {(ran.IsError == true ? "yes" : "no")}");
    foreach (string line in Lines(answer, "standardOutput").Concat(Lines(answer, "standardError")))
    {
        Console.WriteLine($"  {line}");
    }
}

// curl-mcp answers in text, as JSON.
static JsonElement Answer(CallToolResult result) =>
    JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;

// One stream of the curl tool's answer, line by line, without line ends.
static IEnumerable<string> Lines(JsonElement answer, string stream) =>
    (answer.GetProperty(stream).GetProperty("content").GetString() ?? "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r'));

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
