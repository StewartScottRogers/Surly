// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests an upload pipeline: report.csv goes over FTP to an inbox, over HTTPS to staging, then
// into an S3 bucket, one curl-mcp call per step. surl-mcp cuts the S3 step's connection once; the
// agent runs that step again and not the first two, then reads the server's record.
// server.pem, server.key and report.csv are in the folder.
// Run: dotnet run program.cs -- <case>
//   resume   a failed step is run once more, alone (the default)
//   stop     the pipeline as first written, which stops at the first failure
//   events   the default run, then the fault events and the rule's remaining uses
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

// One server for the three services, in memory: FTP, HTTPS and S3, the job's login and its key pair,
// uploads on, and yesterday's report in each folder.
const string Yesterday = "region,total\r\nnorth,1180\r\nsouth,940\r\n";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0", "https://127.0.0.1:0", "s3://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "nightly:s3cret", "--user", "AKIAIOSFODNN7EXAMPLE:s3cret", "--allow-uploads" },
    ["files"] = new Dictionary<string, string> { ["inbox/2026-11-23.csv"] = Yesterday, ["staging/2026-11-23.csv"] = Yesterday, ["archive/2026-11-23.csv"] = Yesterday },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
int[] ports = server.GetProperty("bound_urls").EnumerateArray().Select(u => new Uri(u.GetString()!).Port).ToArray();

// The fault: cut an S3 answer after 1 byte, closing cleanly, once.
await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault"] = "drop;scheme=s3;after-bytes=1;times=1", ["close"] = "graceful" });

// The pipeline's steps, each one curl call.
const string Name = "2026-11-24.csv";
var steps = new (string Name, string[] Args)[]
{
    ("ftp", ["-sS", "--ssl-reqd", "--cacert", "server.pem", "-u", "nightly:s3cret", "-T", "report.csv", $"ftp://127.0.0.1:{ports[0]}/inbox/{Name}"]),
    ("https", ["-sS", "-f", "--cacert", "server.pem", "-u", "nightly:s3cret", "-T", "report.csv", "-w", "%{http_code}", $"https://127.0.0.1:{ports[1]}/staging/{Name}"]),
    ("s3", ["-sS", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{ports[2]}", "-u", "AKIAIOSFODNN7EXAMPLE:s3cret", "-T", "report.csv", "-w", "%{http_code}", $"s3://archive/{Name}"]),
};

// The agent runs the steps in order and, unless told to stop, runs a failed step once more, alone.
foreach ((string name, string[] argv) in steps)
{
    (int code, string said) = await Curl(argv);
    Console.WriteLine($"{name}: exitCode {code}{said}");
    if (code != 0 && which == "stop")
    {
        Console.WriteLine("the pipeline stops");
        break;
    }

    if (code != 0)
    {
        (code, said) = await Curl(argv);
        Console.WriteLine($"{name} again: exitCode {code}{said}");
    }
}

// What the server recorded: its exchanges, by scheme.
foreach (string scheme in new[] { "ftp", "https", "s3" })
{
    CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = scheme });
    Console.WriteLine($"{scheme} exchanges: {got.StructuredContent!.Value.GetProperty("exchanges").GetArrayLength()}");
}

// The archive's copy, read from the server's store.
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = $"/archive/{Name}" });
Console.WriteLine($"archive: {(read.StructuredContent!.Value.GetProperty("text").GetString() == File.ReadAllText("report.csv") ? "identical to report.csv" : "different")}");

if (which == "events")
{
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "fault_applied" } });
    foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
    {
        Console.WriteLine($"fault_applied: rule {e.GetProperty("rule_id")}, {e.GetProperty("fault_kind")}, stage {e.GetProperty("stage")}, on the {e.GetProperty("target").GetString()!.Split(':')[0]} listener");
    }

    CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = id });
    foreach (JsonElement f in faults.StructuredContent!.Value.GetProperty("faults").EnumerateArray())
    {
        Console.WriteLine($"{f.GetProperty("fault_id")}: {f.GetProperty("item")}, remaining {f.GetProperty("remaining")}");
    }
}

return 0;

// One curl tool call: its exit code and, after a comma, what it printed (stdout, then stderr).
async Task<(int, string)> Curl(string[] argv)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string printed = (answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString() + " "
        + answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()).Replace("\r", "").Replace("\n", " ").Trim();
    return (answer.RootElement.GetProperty("exitCode").GetInt32(), printed.Length > 0 ? ", " + printed : "");
}
