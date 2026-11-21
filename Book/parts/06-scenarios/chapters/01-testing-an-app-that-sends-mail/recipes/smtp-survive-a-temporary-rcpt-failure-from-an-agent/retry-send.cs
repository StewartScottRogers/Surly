// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests that a sender retries a temporary refusal: surl-mcp runs the mail server and adds a
// fault rule on RCPT, curl-mcp's curl tool sends as the app would, retried by the agent on a 4xx,
// and surl-mcp's tools show what the server applied and stored.
// Run: dotnet run retry-send.cs -- <case>
//   temporary   the first RCPT is answered 451; the sender retries 4xx refusals (the default)
//   no-retry    the same rule; the sender gives up on the first refusal
//   permanent   every RCPT is answered 550
//   bad-reply   a rule whose reply is not an SMTP error
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "temporary";

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

// SMTP on a free port for example.com, a mailbox for the app and one for ann.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The fault rule, added to the running server: one surl --fault item.
string fault = which switch
{
    "permanent" => "error;scheme=smtp;command=RCPT;reply=550 5.1.1 mailbox unavailable",
    "bad-reply" => "error;scheme=smtp;command=RCPT;reply=250 OK",
    _ => "error;scheme=smtp;command=RCPT;reply=451 4.3.0 try later;times=1",
};
CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault"] = fault });
if (added.IsError == true)
{
    Console.WriteLine($"add_fault refused: {added.Content.OfType<TextContentBlock>().Single().Text}");
    return 1;
}

Console.WriteLine($"add_fault: {added.StructuredContent!.Value.GetProperty("fault_id")}");

// The sender: the curl tool sends order.eml; a 4xx refusal is tried again after a second, at most three tries.
int tries = which == "no-retry" ? 1 : 3;
for (int attempt = 1; attempt <= tries; attempt++)
{
    JsonElement sent = await Curl("--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com",
        "--mail-rcpt", "ann@example.com", "-T", "order.eml", "-w", "%{response_code}", smtp + "example.com");
    string code = sent.GetProperty("standardOutput").GetProperty("content").GetString()!;
    Console.WriteLine($"try {attempt}: curl exit {sent.GetProperty("exitCode")}, reply {code}");
    if (sent.GetProperty("exitCode").GetInt32() == 0 || !code.StartsWith('4'))
    {
        break;
    }

    await Task.Delay(TimeSpan.FromSeconds(1));
}

// What the server applied and stored, read without a client.
CallToolResult faults = await surl.CallToolAsync("list_faults", new Dictionary<string, object?> { ["server_id"] = id });
JsonElement rule = faults.StructuredContent!.Value.GetProperty("faults")[0];
Console.WriteLine($"list_faults: {rule.GetProperty("fault_id")} remaining {rule.GetProperty("remaining").GetRawText()}");
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "fault_applied" } });
Console.WriteLine($"get_events: {events.StructuredContent!.Value.GetProperty("events").GetArrayLength()} fault_applied");
CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
int count = boxes.StructuredContent!.Value.GetProperty("mailboxes")[0].GetProperty("message_count").GetInt32();
Console.WriteLine($"list_mailboxes: {count} message(s) for ann");
return count == 1 ? 0 : 1;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
