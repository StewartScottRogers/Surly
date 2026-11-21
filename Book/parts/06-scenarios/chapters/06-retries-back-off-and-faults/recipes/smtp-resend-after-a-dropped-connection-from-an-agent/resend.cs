// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests that a sender connects again after a dropped connection and that one copy of the mail
// arrives: surl-mcp runs the mail server and adds a drop rule, curl-mcp's curl tool sends as the app
// would, tried again by the agent on an error, and surl-mcp's tools show what the server stored.
// Run: dotnet run resend.cs -- <case>
//   greeting   the first connection is dropped before the greeting; the sender tries once more
//   events     the same, and the events and exchanges the server kept
//   twice      the first two connections are dropped: a send with no retry, then one with one retry
//   data       the first DATA command drops the connection, before the message is accepted
//   quit       the first QUIT drops the connection, after the message was accepted
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "greeting";

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

// The drop rule, added to the running server: one surl --fault item.
string fault = mode switch
{
    "twice" => "drop;scheme=smtp;times=2",
    "data" => "drop;scheme=smtp;stage=command;command=DATA;times=1",
    "quit" => "drop;scheme=smtp;stage=command;command=QUIT;times=1",
    _ => "drop;scheme=smtp;times=1",
};
CallToolResult added = await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault"] = fault });
Console.WriteLine($"add_fault: {added.StructuredContent!.Value.GetProperty("fault_id")}");

// The app's sender: up to 1 + retries tries, the next one after any error, one second later.
long seen = 0;
async Task<bool> Send(string label, int retries)
{
    for (int attempt = 1; attempt <= 1 + retries; attempt++)
    {
        JsonElement sent = await Curl("--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com",
            "--mail-rcpt", "ann@example.com", "-T", "order.eml", "-w", "%{response_code}", smtp + "example.com");
        int exit = sent.GetProperty("exitCode").GetInt32();
        Console.WriteLine($"{label}, try {attempt}: curl exit {exit}, reply {sent.GetProperty("standardOutput").GetProperty("content").GetString()}");
        if (exit == 0)
        {
            // Delivery is asynchronous: an accepted send is a delivery event, and the wait for it has a bound.
            CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
            {
                ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["after_sequence"] = seen, ["timeout_ms"] = 10000,
            });
            JsonElement answer = waited.StructuredContent!.Value;
            if (answer.TryGetProperty("timed_out", out _))
            {
                return false;
            }

            seen = answer.GetProperty("event").GetProperty("sequence").GetInt64();
            return true;
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    return true;
}

bool delivered;
if (mode == "twice")
{
    await Send("first send", 0);
    Console.WriteLine($"list_mailboxes: ann@example.com holds {await Held()}");
    delivered = await Send("second send", 1);
}
else
{
    delivered = await Send("send", 1);
}

if (!delivered)
{
    Console.WriteLine("no delivery within 10 s");
    return 1;
}

// The test: what the store holds and what the server kept, read without a client.
Console.WriteLine($"list_mailboxes: ann@example.com holds {await Held()}");
if (mode == "events")
{
    CallToolResult applied = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "fault_applied" } });
    JsonElement first = applied.StructuredContent!.Value.GetProperty("events")[0];
    Console.WriteLine($"get_events: {applied.StructuredContent!.Value.GetProperty("events").GetArrayLength()} fault_applied, {first.GetProperty("fault_kind")} by {first.GetProperty("rule_id")}");
    CallToolResult stored = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "store_changed" } });
    Console.WriteLine($"get_events: {stored.StructuredContent!.Value.GetProperty("events").GetArrayLength()} store_changed");
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
    Console.WriteLine($"get_exchanges: {exchanges.StructuredContent!.Value.GetProperty("exchanges").GetArrayLength()} smtp connections");
}

return 0;

// How many messages ann's INBOX holds, read from the store.
async Task<int> Held()
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
    return boxes.StructuredContent!.Value.GetProperty("mailboxes")[0].GetProperty("message_count").GetInt32();
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
