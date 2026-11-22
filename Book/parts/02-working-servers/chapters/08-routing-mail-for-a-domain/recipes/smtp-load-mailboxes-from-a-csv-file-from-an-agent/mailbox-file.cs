// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent writes a mailbox CSV into its folder, starts surl-mcp from it, reads the record the refusal
// names, fixes that line and starts again; then it uses the mailboxes through curl-mcp's curl tool.
// Run: dotnet run mailbox-file.cs [-- twice | column | missing | outside]
//   (no argument)  forty testers, ops, and one bad record: refuse, fix, start, send, read, log in
//   twice          a second file that repeats tester05
//   column         a header naming a column surl does not know
//   missing        a file that is not there
//   outside        a file named outside the folder surl-mcp is confined to
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "fix";

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

// The file the agent writes: forty testers (the last ten IMAP only), ops with a quoted password,
// a blank line, and a record for carol in a domain the server does not serve.
var lines = new List<string> { "address,password,protocols" };
for (int i = 1; i <= 40; i++)
{
    lines.Add($"tester{i:00}@example.com,pass{i:00},{(i <= 30 ? "" : "imap")}");
}

lines.AddRange(["ops@example.com,\"s3cret,too\",imap", "", "carol@elsewhere.org,carolpass,"]);
File.WriteAllText("mailboxes.csv", mode == "column" ? "address,pass\nann@example.com,annpass\n" : string.Join("\n", lines) + "\n");
File.WriteAllText("other.csv", "address,password\ntester05@example.com,another\n");

var arguments = new List<string> { "-v", "--mail-domain", "example.com", "--mailbox-file", mode switch
{
    "missing" => "missing.csv",
    "outside" => "../mailboxes.csv",
    _ => "mailboxes.csv",
} };
if (mode == "twice")
{
    // Fixed first, so the only mistake is the repeat.
    lines[^1] = "carol@example.com,carolpass,";
    File.WriteAllText("mailboxes.csv", string.Join("\n", lines) + "\n");
    arguments.AddRange(["--mailbox-file", "other.csv"]);
}

CallToolResult started = await Start();
if (mode != "fix")
{
    return 1;
}

// The agent reads the line number and the address from the refusal, and fixes that record.
Match named = Regex.Match(started.StructuredContent!.Value.GetProperty("lines")[0].GetString()!, @"line (\d+): mailbox (\S+)@\S+ is not in any --mail-domain");
int lineNumber = int.Parse(named.Groups[1].Value);
lines[lineNumber - 1] = lines[lineNumber - 1].Replace("@elsewhere.org", "@example.com");
File.WriteAllText("mailboxes.csv", string.Join("\n", lines) + "\n");
Console.WriteLine($"fixed line {lineNumber}: {lines[lineNumber - 1]}");

started = await Start();
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;
string imap = server.GetProperty("bound_urls")[1].GetString()!;
string pop3 = server.GetProperty("bound_urls")[2].GetString()!;
string[] log = server.GetProperty("log_lines").EnumerateArray().Select(l => l.GetString()!).ToArray();
Console.WriteLine($"testers in log_lines: {log.Count(l => l.StartsWith("* Mailbox tester"))}");
Console.WriteLine($"log: {log.Single(l => l.StartsWith("* Mailbox tester31@example.com:"))}");

// tester01 mails tester02; the agent waits for the delivery, then reads the subject over IMAP.
await Curl("-u", "tester01@example.com:pass01", "--mail-from", "tester01@example.com", "--mail-rcpt", "tester02@example.com", "-T", "note.eml", smtp + "ci.example.com");
await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = "tester02@example.com", ["timeout_ms"] = 10000,
});
Console.WriteLine($"tester02 over IMAP: {Out(await Curl("-u", "tester02@example.com:pass02", imap + "INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)"))}");

JsonElement refused = await Curl("-u", "tester31@example.com:pass31", pop3);
Console.WriteLine($"tester31 over POP3: exitCode {refused.GetProperty("exitCode")}, {refused.GetProperty("standardError").GetProperty("content").GetString()!.Trim()}");
Console.WriteLine($"tester31 over IMAP: {Out(await Curl("-u", "tester31@example.com:pass31", "-X", "STATUS INBOX (MESSAGES)", imap))}");
Console.WriteLine($"tester02 over POP3: {Out(await Curl("-u", "tester02@example.com:pass02", pop3))}");
Console.WriteLine($"ops over IMAP: {Out(await Curl("-u", "ops@example.com:s3cret,too", "-X", "STATUS INBOX (MESSAGES)", imap))}");
return 0;

// start_server with the same arguments each time; a refusal is printed as the agent reads it.
async Task<CallToolResult> Start()
{
    CallToolResult result = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "pop3://127.0.0.1:0" },
        ["arguments"] = arguments.ToArray(),
    });
    if (result.IsError == true)
    {
        JsonElement why = result.StructuredContent!.Value;
        Console.WriteLine(why.TryGetProperty("exit_code", out JsonElement code)
            ? $"start_server: tool error, exit_code {code} ({why.GetProperty("exit_code_name").GetString()}), {why.GetProperty("lines")[0].GetString()}"
            : $"start_server: tool error, {why.GetProperty("refused").GetString()}");
    }

    return result;
}

string Out(JsonElement answer) => answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
