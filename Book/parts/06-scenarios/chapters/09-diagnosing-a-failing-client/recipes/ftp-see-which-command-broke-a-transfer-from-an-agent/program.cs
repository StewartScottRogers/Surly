// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An FTP job that fails now and then runs through curl-mcp's curl tool against a surl-mcp server whose fault rules
// answer its first upload 451 and drop its first download. The agent reads the server's record and reports, for each
// session that ended on a failure, the job's last command and the reply that ended it. Run: dotnet run program.cs -- <case>
//   runs      what each of three runs of the job got back
//   failed    the failed sessions, from get_exchanges, with the fault rule get_events names for each
//   passing   how many sessions sent STOR nightly.csv
//   real      a server with no fault rule and no --allow-uploads: one run, then the failed sessions
//   locked    a server with no anonymous login: one run
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args[0];
bool faulty = which is "runs" or "failed" or "passing";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The job uploads nightly.csv from here.
string here = Directory.GetCurrentDirectory();
File.WriteAllText("nightly.csv", "id,total\n1,42\n");
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

// surl-mcp: the stand-in, with two fault rules spent once each on the faulty server.
var serverArguments = new List<string>();
if (which != "locked") serverArguments.Add("--allow-anonymous");
if (which != "real") serverArguments.Add("--allow-uploads");
if (faulty)
{
    serverArguments.AddRange(["--fault", "error;scheme=ftp;command=STOR;path=/outbox;reply=451 Local error in processing;times=1"]);
    serverArguments.AddRange(["--fault", "drop;stage=command;command=RETR;path=/inbox;times=1"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = serverArguments,
    ["files"] = new Dictionary<string, string> { ["outbox/README.txt"] = "Uploads land here.\n", ["inbox/orders.csv"] = "order,qty\n7,3\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The job runs three times on the faulty server, once on the others.
for (int run = 1; run <= (faulty ? 3 : 1); run++)
{
    string result = await Job();
    if (which is "runs" or "real" or "locked") Console.WriteLine($"run {run}: {result}");
}

await Settled();

switch (which)
{
    case "failed":
    case "real":
        await Failed();
        break;
    case "passing":
        int sent = (await Exchanges()).Count(x => Commands(x).Contains("STOR nightly.csv"));
        Console.WriteLine($"{sent} sessions sent STOR nightly.csv");
        break;
}

return 0;

// The FTP job: uploads nightly.csv to outbox/, then fetches inbox/orders.csv. A failed step ends the run.
async Task<string> Job()
{
    string failed = await Curl(["-sS", "-T", "nightly.csv", url + "outbox/nightly.csv"]);
    if (failed != "") return failed;
    failed = await Curl(["-sS", "-o", "orders.csv", url + "inbox/orders.csv"]);
    return failed != "" ? failed : "passed";
}

// The record is kept as exchanges run: wait, with a bound, until every exchange that started has ended.
async Task Settled()
{
    for (int tries = 0; tries < 50; tries++)
    {
        if ((await Events("exchange_started")).Count == (await Events("exchange_ended")).Count) return;
        await Task.Delay(200);
    }
}

// Each session that did not end on a 2xx or 3xx reply to its last command, and the fault rule that acted in it.
async Task Failed()
{
    List<JsonElement> faults = await Events("fault_applied");
    bool any = false;
    foreach (JsonElement exchange in await Exchanges())
    {
        (string? command, string? reply) = LastWords(exchange);
        if (command is null || reply is ['2' or '3', ..]) continue;
        any = true;
        long id = exchange.GetProperty("exchange_id").GetInt64();
        Console.WriteLine($"exchange {id}: {command} -> {reply ?? "no reply"}");
        foreach (JsonElement fault in faults.Where(f => f.GetProperty("exchange_id").GetInt64() == id))
        {
            Console.WriteLine($"  fault {fault.GetProperty("rule_id")}: {fault.GetProperty("fault_kind")} at {fault.GetProperty("stage")} stage, on {fault.GetProperty("target")}");
        }
    }

    if (!any) Console.WriteLine("no failed session in the record");
}

// The last command a session sent before QUIT, and the last reply to it, if any. The chunks are in the order they
// crossed; a file's bytes cross between a 1xx reply and the next reply, and are not commands.
(string? Command, string? Reply) LastWords(JsonElement exchange)
{
    string? command = null, reply = null;
    bool inData = false;
    foreach ((string direction, string text) in Chunks(exchange))
    {
        if (direction == "sent" && Regex.IsMatch(text, @"^[1-5]\d\d "))
        {
            if (command is not null) reply = text.TrimEnd();
            inData = text[0] == '1';
        }
        else if (direction == "received" && !inData)
        {
            foreach (string line in text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            {
                if (line == "QUIT") return (command, reply);
                (command, reply) = (line, null);
            }
        }
    }

    return (command, reply);
}

// The commands a session sent, a file's bytes left out.
List<string> Commands(JsonElement exchange)
{
    var commands = new List<string>();
    bool inData = false;
    foreach ((string direction, string text) in Chunks(exchange))
    {
        if (direction == "sent" && Regex.IsMatch(text, @"^[1-5]\d\d ")) inData = text[0] == '1';
        else if (direction == "received" && !inData) commands.AddRange(text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
    }

    return commands;
}

IEnumerable<(string Direction, string Text)> Chunks(JsonElement exchange) => exchange.GetProperty("chunks").EnumerateArray()
    .Select(c => (c.GetProperty("direction").GetString()!, c.GetProperty("data").GetString()!));

// get_exchanges answers each exchange with its chunks, the bytes in the order they crossed.
async Task<List<JsonElement>> Exchanges()
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
    return [.. result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Select(x => x.Clone())];
}

// get_events answers the events kept of one kind, oldest first.
async Task<List<JsonElement>> Events(string kind)
{
    CallToolResult result = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = server, ["kinds"] = new[] { kind } });
    return [.. result.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.Clone())];
}

// The curl tool runs curl's own arguments, one list item each, with no shell. A refusal is an answer with a
// non-zero exitCode, not a failure of the call; this answers "" for exit 0, else the exit code and curl's message.
async Task<string> Curl(string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    JsonElement error = answer.GetProperty("standardError");
    string said = error.ValueKind == JsonValueKind.Null ? "" : error.GetProperty("content").GetString()!.Trim();
    return answer.GetProperty("exitCode").GetInt32() == 0 ? "" : $"exit {answer.GetProperty("exitCode")} {said}";
}
