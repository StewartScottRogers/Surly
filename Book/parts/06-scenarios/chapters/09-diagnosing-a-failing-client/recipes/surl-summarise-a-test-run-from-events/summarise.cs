// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent summarises a test run from surl-mcp's events: logins, uploads, mail delivered and
// faults applied, read from what the server kept rather than from the clients' side.
// Run: dotnet run summarise.cs [-- wrong-ftp-password | no-plaintext]
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "pass";

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

// The servers under test: HTTP, SMTP and FTP in one surl, one account, one mailbox, and a fault
// rule that answers the first request for /flaky.txt with 503.
string[] options =
[
    "--user", "alice:s3cret", "--allow-uploads", "--allow-plaintext-auth",
    "--mail-domain", "example.com", "--mailbox", "app@example.com:secret",
    "--fault", "error;scheme=http;reply=503;path=/flaky.txt;times=1",
];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0", "smtp://127.0.0.1:0", "ftp://127.0.0.1:0" },
    ["arguments"] = mode == "no-plaintext" ? options.Where(o => o != "--allow-plaintext-auth").ToArray() : options,
    ["files"] = new Dictionary<string, string> { ["report.txt"] = "monthly report\n", ["flaky.txt"] = "fine now\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string[] urls = [.. server.GetProperty("bound_urls").EnumerateArray().Select(url => url.GetString()!)];

// The test run, as the app's tests make it: two fetches (one with a wrong password), a fetch that
// meets the fault and retries, an FTP upload and a mail.
File.WriteAllText("upload.csv", "id,total\n1,42\n");
File.WriteAllText("mail.txt", "Subject: Run finished\r\n\r\nAll done.\r\n");
string ftpPassword = mode == "wrong-ftp-password" ? "wrong" : "s3cret";
Console.WriteLine("curl exits: " + string.Join(" ", [
    await Curl("-s", "-o", "a.out", "--digest", "-u", "alice:s3cret", urls[0] + "report.txt"),
    await Curl("-s", "-o", "b.out", "--digest", "-u", "alice:wrong", urls[0] + "report.txt"),
    await Curl("-s", "-o", "c.out", "--digest", "-u", "alice:s3cret", "--retry", "1", urls[0] + "flaky.txt"),
    await Curl("-s", "-u", $"alice:{ftpPassword}", "-T", "upload.csv", urls[2] + "upload.csv"),
    await Curl("-s", "-u", "app@example.com:secret", "--mail-from", "app@example.com", "--mail-rcpt", "app@example.com",
        "-T", "mail.txt", urls[1] + "example.com"),
]));

// Every event the server kept, page by page, once every exchange has ended (for up to 10 s).
List<JsonElement> events = await ReadEvents();
for (var waited = System.Diagnostics.Stopwatch.StartNew();
     Count("exchange_started") != Count("exchange_ended") && waited.Elapsed.TotalSeconds < 10;)
{
    await Task.Delay(100);
    events = await ReadEvents();
}

// Which scheme each exchange came in on, to name it beside the events that carry only exchange_id.
Dictionary<long, string> scheme = events.Where(e => Kind(e) == "exchange_started")
    .ToDictionary(e => e.GetProperty("exchange_id").GetInt64(), e => e.GetProperty("scheme").GetString()!);

// The summary.
var logins = events.Where(e => Kind(e) == "login_judged").ToList();
Console.WriteLine($"exchanges: {Count("exchange_started")}");
Console.WriteLine($"logins: {logins.Count(e => e.GetProperty("is_accepted").GetBoolean())} accepted, " +
    $"{logins.Count(e => !e.GetProperty("is_accepted").GetBoolean())} refused");
foreach (JsonElement refused in logins.Where(e => !e.GetProperty("is_accepted").GetBoolean()))
{
    Console.WriteLine($"  refused: {refused.GetProperty("account")} over {refused.GetProperty("scheme")} ({refused.GetProperty("mechanism")})");
}

Report("uploads", events.Where(e => Kind(e) == "store_changed" && Text(e, "store") == "content" && Text(e, "change") == "written")
    .Select(e => $"{Text(e, "name")}, {e.GetProperty("byte_count")} bytes over {scheme[e.GetProperty("exchange_id").GetInt64()]}"));
Report("mail delivered", events.Where(e => Kind(e) == "store_changed" && Text(e, "store") == "mail" && Text(e, "change") == "delivered")
    .Select(e => $"{Text(e, "name")}, {e.GetProperty("byte_count")} bytes over {scheme[e.GetProperty("exchange_id").GetInt64()]}"));
Report("faults applied", events.Where(e => Kind(e) == "fault_applied")
    .Select(e => $"{Text(e, "rule_id")} {Text(e, "fault_kind")} at {Text(e, "target")} over {scheme[e.GetProperty("exchange_id").GetInt64()]}"));
return 0;

int Count(string kind) => events.Count(e => Kind(e) == kind);

static string Kind(JsonElement e) => e.GetProperty("kind").GetString()!;

static string Text(JsonElement e, string name) => e.GetProperty(name).GetString()!;

static void Report(string what, IEnumerable<string> lines)
{
    List<string> all = [.. lines];
    Console.WriteLine($"{what}: {(all.Count == 0 ? "none" : all.Count)}");
    all.ForEach(line => Console.WriteLine($"  {line}"));
}

async Task<List<JsonElement>> ReadEvents()
{
    var kept = new List<JsonElement>();
    long after = 0;
    while (true)
    {
        JsonElement page = (await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = serverId, ["after_sequence"] = after, ["max_events"] = 500,
        })).StructuredContent!.Value;
        foreach (JsonElement e in page.GetProperty("events").EnumerateArray())
        {
            kept.Add(e.Clone());
            after = e.GetProperty("sequence").GetInt64();
        }

        if (!page.GetProperty("has_more").GetBoolean())
        {
            return kept;
        }
    }
}

// The curl tool with these arguments; answers curl's exit code.
async Task<int> Curl(params string[] arguments)
{
    CallToolResult answer = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    return JsonDocument.Parse(answer.Content.OfType<TextContentBlock>().Single().Text).RootElement.GetProperty("exitCode").GetInt32();
}
