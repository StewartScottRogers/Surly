// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An FTP download hangs after the login. The job runs through curl-mcp's curl tool against a surl-mcp server
// whose delay rule holds one reply, as a broken network would; the agent then reads the session from
// get_exchanges and names the step that never completed. Run: dotnet run find-the-hang.cs -- <case>
//   passive  the server holds its answer to EPSV, the client's request for a passive data connection (the default)
//   active   the same server, the job in active mode (-P -)
//   retr     the server holds its answer to RETR instead
//   locked   a server without --allow-anonymous: the job only
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "passive";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The job downloads into it.
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

// The stand-in: anonymous FTP with one file, and a rule that holds one command's reply for up to 30 s.
string held = which == "retr" ? "RETR" : "EPSV";
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = (string[])[.. (which == "locked" ? Array.Empty<string>() : ["--allow-anonymous"]), "--fault", $"delay;stage=command;command={held};ms=30000"],
    ["files"] = new Dictionary<string, string> { ["report.csv"] = "id,total\n1,42\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The job, as it runs every night: a download that gives up after 3 s.
File.Delete("report.csv");
var job = new List<string> { "-s", "-S", "-m", "3", "-o", "report.csv" };
if (which == "active") job.AddRange(["-P", "-"]);
job.Add(url + "report.csv");
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = job.ToArray() });
JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string said = answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
Console.WriteLine($"job: curl exit {answer.GetProperty("exitCode")}{(said == "" ? "" : ", " + said)}");
if (File.Exists("report.csv")) Console.Write($"report.csv: {File.ReadAllText("report.csv")}");
if (which == "locked") return 0;

// The record is whole once the exchange has ended: wait for that, at most 10 s.
CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "exchange_ended" }, ["after_sequence"] = 0, ["timeout_ms"] = 10000,
});
JsonElement end = ended.StructuredContent!.Value.GetProperty("event");
DateTimeOffset endedAt = DateTimeOffset.Parse(end.GetProperty("at").GetString()!);
CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
{
    ["server_id"] = server, ["after_exchange_id"] = end.GetProperty("exchange_id").GetInt64() - 1, ["max_exchanges"] = 1,
});
JsonElement chunks = recorded.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks");

// Each command the client sent, with the first reply after it and how long that reply took.
var steps = new List<(string Command, string Reply, double Seconds, DateTimeOffset RepliedAt)>();
string? command = null;
DateTimeOffset sentAt = default;
foreach (JsonElement chunk in chunks.EnumerateArray())
{
    string data = chunk.GetProperty("data").GetString()!.TrimEnd('\r', '\n');
    DateTimeOffset at = DateTimeOffset.Parse(chunk.GetProperty("at").GetString()!);
    if (chunk.GetProperty("direction").GetString() == "received")
    {
        command = data;
        sentAt = at;
    }
    else if (command is not null && data.Length > 3 && char.IsAsciiDigit(data[0]) && data[3] is ' ' or '-')
    {
        steps.Add((command, data, (at - sentAt).TotalSeconds, at));
        command = null;
    }
}

Console.WriteLine($"commands: {string.Join(", ", steps.Select(s => s.Command.Split(' ')[0]))}{(command is null ? "" : $", {command.Split(' ')[0]} (no reply)")}");
var slowest = steps.MaxBy(s => s.Seconds);
if (slowest.Seconds < 2)
{
    Console.WriteLine($"no step held: every reply came within {Math.Ceiling(slowest.Seconds)} s");
}
else
{
    bool asClientLeft = (endedAt - slowest.RepliedAt).TotalSeconds < 1;
    Console.WriteLine($"stuck at: {slowest.Command}; its reply came {slowest.Seconds:0} s later{(asClientLeft ? ", as the exchange ended" : "")}: {slowest.Reply}");
    var before = steps.TakeWhile(s => s.Command != slowest.Command).ToList();
    Console.WriteLine($"before it: {string.Join(", ", before.Select(s => s.Command.Split(' ')[0]))}, each answered within {Math.Ceiling(before.Max(s => s.Seconds))} s");
    Console.WriteLine($"RETR sent: {(steps.Any(s => s.Command.StartsWith("RETR")) ? "yes" : "no")}");
}

// A download that completed ends with 226 on the control connection.
string? done = chunks.EnumerateArray().Where(c => c.GetProperty("direction").GetString() == "sent")
    .Select(c => c.GetProperty("data").GetString()!.TrimEnd('\r', '\n')).FirstOrDefault(d => d.StartsWith("226 "));
Console.WriteLine($"transfer complete: {done ?? "no 226 sent"}");

// The stand-in also says which of its rules acted: the confirmation a real server cannot give.
CallToolResult faults = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = server, ["kinds"] = new[] { "fault_applied" },
});
foreach (JsonElement e in faults.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"fault_applied: {e.GetProperty("rule_id")} {e.GetProperty("fault_kind")} at stage {e.GetProperty("stage")} on {e.GetProperty("target")}, exchange {e.GetProperty("exchange_id")}");
}

return 0;
