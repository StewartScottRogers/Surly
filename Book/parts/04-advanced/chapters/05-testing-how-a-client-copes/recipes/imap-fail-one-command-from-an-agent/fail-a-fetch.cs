// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests how a mail app copes with one IMAP command answered NO: surl-mcp runs a server with a
// fault rule, curl-mcp's curl tool fetches as the app would, and surl-mcp's events and exchanges show
// the server's side.
// Run: dotnet run fail-a-fetch.cs -- <case>
//   first-fails  the first fetch is answered NO; the same fetch again works (the default)
//   search       a SEARCH works while the rule for the fetch still waits
//   retry-alone  curl's --retry alone does not retry the NO
//   retry-all    --retry-all-errors does
//   select       the rule is for SELECT, so the failure comes before any fetch
//   reply        the server's side of the refused fetch, from get_exchanges
//   log          the start warning and the fault_applied event
//   count        list_faults after two fetches
//   late-rule    no rule at start; add_fault during the test, then remove_fault
//   bad-shape    a rule whose reply is not in IMAP's shape
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "first-fails";

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

// The server: IMAP for example.com with two accounts, and the rules the case needs. The rules are
// per account, so the retry case can try two ways of retrying on one server.
const string NoFetch = "reply=NO [SERVERBUG] fetch failed;times=1";
var arguments = new List<string>
{
    "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass",
};
if (which == "bad-shape")
{
    arguments.AddRange(["--fault", "error;scheme=imap;command=FETCH;reply=250 OK"]);
}
else if (which == "select")
{
    arguments.AddRange(["--fault", "error;scheme=imap;command=SELECT;account=ann@example.com;reply=NO [UNAVAILABLE] store busy;times=1"]);
}
else if (which != "late-rule")
{
    arguments.AddRange(["--fault", $"error;scheme=imap;command=FETCH;account=ann@example.com;{NoFetch}"]);
    arguments.AddRange(["--fault", $"error;scheme=imap;command=FETCH;account=bob@example.com;{NoFetch}"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imap://127.0.0.1:0" },
    ["arguments"] = arguments,
});
if (started.IsError == true)
{
    JsonElement refused = started.StructuredContent!.Value;
    Console.WriteLine($"start_server refused: {refused.GetProperty("exit_code")} {refused.GetProperty("exit_code_name")}: {refused.GetProperty("lines")[0]}");
    return 2;
}

JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string imap = server.GetProperty("bound_urls")[0].GetString()!;

// The mail the app will fetch, put in each account's store with no client.
foreach (string to in new[] { "ann@example.com", "bob@example.com" })
{
    await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = to,
        ["message"] = $"From: shop@example.com\r\nTo: {to}\r\nSubject: Order 1042 has shipped\r\n\r\nYour order is on its way.\r\n",
    });
}

const string Text = "INBOX;UID=1;SECTION=TEXT";
switch (which)
{
    case "search":
        Console.WriteLine($"search: {Describe(await Fetch("ann", "INBOX?ALL"))}");
        Console.WriteLine($"fetch: {Describe(await Fetch("ann", Text))}");
        break;
    case "retry-alone":
        // curl's own retries: --retry alone does not count a NO as transient.
        Console.WriteLine($"ann, --retry: {Describe(await Fetch("ann", Text, "--retry", "2", "--retry-delay", "1", "-w", "retries %{num_retries}\n"))}");
        break;
    case "retry-all":
        Console.WriteLine($"ann, --retry-all-errors: {Describe(await Fetch("ann", Text, "--retry", "2", "--retry-all-errors", "--retry-delay", "1", "-w", "retries %{num_retries}\n"))}");
        break;
    case "select":
        Console.WriteLine($"search: {Describe(await Fetch("ann", "INBOX?ALL"))}");
        Console.WriteLine($"search again: {Describe(await Fetch("ann", "INBOX?ALL"))}");
        break;
    case "reply":
        await Fetch("ann", Text);
        // The server records an exchange a moment after curl has its answer: ask until every exchange
        // that started has ended, for at most 10 s, before reading the record.
        for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
        {
            JsonElement seen = (await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "exchange_started", "exchange_ended" } })).StructuredContent!.Value;
            var kinds = seen.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("kind").GetString()).ToList();
            if (kinds.Count(k => k == "exchange_started") == kinds.Count(k => k == "exchange_ended")) break;
        }

        CallToolResult exchanges =await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id });
        var sent = new StringBuilder();
        foreach (JsonElement exchange in exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
        {
            foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
            {
                sent.Append(chunk.GetProperty("direction").GetString() == "sent" ? chunk.GetProperty("data").GetString() : "");
            }
        }

        foreach (string line in sent.ToString().Split("\r\n").Where(l => Regex.IsMatch(l, "^A00[34] ")))
        {
            Console.WriteLine(line);
        }

        break;
    case "log":
        await Fetch("ann", Text);
        foreach (JsonElement line in server.GetProperty("log_lines").EnumerateArray().Where(l => l.GetString()!.Contains("misbehave")))
        {
            Console.WriteLine(line.GetString());
        }

        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "fault_applied" } });
        foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
        {
            Console.WriteLine($"{e.GetProperty("kind").GetString()}: rule {e.GetProperty("rule_id").GetString()}, {e.GetProperty("fault_kind").GetString()}, stage {e.GetProperty("stage").GetString()}");
        }

        break;
    case "count":
        await Fetch("ann", Text);
        await Fetch("ann", Text);
        foreach (JsonElement rule in (await List()).EnumerateArray())
        {
            Console.WriteLine($"{rule.GetProperty("fault_id").GetString()} remaining: {rule.GetProperty("remaining")}");
        }

        break;
    case "late-rule":
        Console.WriteLine($"before: {Describe(await Fetch("ann", Text))}");
        CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["fault"] = "error;scheme=imap;command=FETCH;reply=NO [SERVERBUG] fetch failed",
        });
        string rule1 = added.StructuredContent!.Value.GetProperty("fault_id").GetString()!;
        Console.WriteLine($"with the rule: {Describe(await Fetch("ann", Text))}");
        Console.WriteLine($"with the rule again: {Describe(await Fetch("ann", Text))}");
        await surl.CallToolAsync("remove_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault_id"] = rule1 });
        Console.WriteLine($"removed {rule1}, rules left: {(await List()).GetArrayLength()}");
        Console.WriteLine($"after: {Describe(await Fetch("ann", Text))}");
        break;
    case "bad-shape":
        break;
    default:
        Console.WriteLine($"fetch 1: {Describe(await Fetch("ann", Text))}");
        Console.WriteLine($"fetch 2: {Describe(await Fetch("ann", Text))}");
        break;
}

return 0;

// One read as the account, with curl's own arguments; -s -S keeps the progress meter out and the errors in.
async Task<JsonElement> Fetch(string name, string path, params string[] more)
{
    string[] arguments = ["-s", "-S", .. more, "-u", $"{name}@example.com:{name}pass", imap + path];
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}

// What the agent sees of one answer: curl's exit code and its error line, or the text it fetched.
string Describe(JsonElement answer)
{
    int exit = answer.GetProperty("exitCode").GetInt32();
    string output = answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
    string shown = output.Replace("\r\n", " | ").Replace("\n", " | ");
    return exit == 0 ? $"exit 0, {shown}" : $"exit {exit}, {error}{(shown == "" ? "" : " | " + shown)}";
}

async Task<JsonElement> List()
{
    CallToolResult listed = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = id });
    return listed.StructuredContent!.Value.GetProperty("faults");
}
