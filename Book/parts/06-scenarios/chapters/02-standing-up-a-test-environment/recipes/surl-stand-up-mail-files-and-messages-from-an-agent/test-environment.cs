// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent stands up an integration test's environment with one surl-mcp start_server call: SMTP and
// IMAP for mail, HTTP for files, MQTT for messages. curl-mcp's curl tool plays the app; surl-mcp's
// read tools check what landed in each store.
// Run: dotnet run test-environment.cs -- <case>
//   right        the app mails, uploads and publishes as it should (the default)
//   wrong-topic  the app publishes on invoices/7 instead of orders/7
//   strict       the server is started without --allow-plaintext-auth
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "right";

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

// One server, four listeners on free ports, one account the app uses on SMTP, HTTP and MQTT.
var arguments = new List<string>
{
    "--allow-uploads", "--mail-domain", "example.com",
    "--mailbox", "app@example.com:apppass;protocols=smtp,http,mqtt", "--mailbox", "ann@example.com:annpass",
    "--topic", "orders/#;retain",
};
if (which != "strict")
{
    arguments.Insert(0, "--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "http://127.0.0.1:0", "mqtt://127.0.0.1:0" },
    ["arguments"] = arguments.ToArray(),
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string[] bound = server.GetProperty("bound_urls").EnumerateArray().Select(u => u.GetString()!).ToArray();

// The app: a mail to ann, the invoice uploaded, an event published.
string topic = which == "wrong-topic" ? "invoices/7" : "orders/7";
Console.WriteLine($"app mailed ann: curl exit {await Curl("--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", "ann@example.com", "-T", "shipped.eml", bound[0] + "example.com")}");
Console.WriteLine($"app uploaded the invoice: curl exit {await Curl("--digest", "-u", "app@example.com:apppass", "-T", "invoice-7.txt", bound[2] + "invoice-7.txt")}");
Console.WriteLine($"app published on {topic}: curl exit {await Curl("-u", "app@example.com:apppass", "-d", "shipped", bound[3] + topic)}");

// A QoS 0 publish is not acknowledged, so curl can return before the broker keeps it. The broker's
// record of the MQTT connection ending comes after what it sent is applied, refused or not: wait for it.
JsonElement ended = await Tool("wait_for_event", new() { ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "mqtt", ["timeout_ms"] = 10000 });
if (!(ended.TryGetProperty("event", out JsonElement inner) ? inner : ended).TryGetProperty("sequence", out _))
{
    throw new TimeoutException("the broker did not end curl's connection in 10 s");
}

// The test: each store read back through surl-mcp, with no client.
JsonElement messages = (await Tool("list_messages", new() { ["owner"] = "ann@example.com", ["mailbox"] = "INBOX" })).GetProperty("messages");
string subject = "(none)";
if (messages.GetArrayLength() > 0)
{
    JsonElement mail = await Tool("read_message", new() { ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = messages[0].GetProperty("uid").GetInt64() });
    subject = mail.GetProperty("text").GetString()!.Split("\r\n").First(l => l.StartsWith("Subject: "))["Subject: ".Length..];
}

JsonElement file = await Tool("read_file", new() { ["path"] = "/invoice-7.txt" });
JsonElement retained = await Tool("read_retained_message", new() { ["topic"] = "orders/7" });
Console.WriteLine($"read_retained_message: {retained.GetRawText()}");

var checks = new (string Store, string Got, string Want)[]
{
    ("mail", subject, "Order 7 shipped"),
    ("file", file.TryGetProperty("text", out JsonElement text) ? text.GetString()!.TrimEnd() : "(none)", "Invoice 7: 3 widgets, 42.00"),
    ("message", retained.TryGetProperty("text", out JsonElement payload) ? payload.GetString()! : "(none)", "shipped"),
};
foreach ((string store, string got, string want) in checks)
{
    Console.WriteLine(got == want ? $"{store}: {got}" : $"FAIL: {store} is {got}, want {want}");
}

return checks.All(c => c.Got == c.Want) ? 0 : 1;

// A surl-mcp tool on this server, answering in structured content.
async Task<JsonElement> Tool(string name, Dictionary<string, object?> toolArguments)
{
    toolArguments["server_id"] = id;
    CallToolResult result = await surl.CallToolAsync(name, toolArguments);
    return result.StructuredContent!.Value;
}

// The curl tool runs curl's own arguments; its exit code is what the app would see. -s keeps the meter out.
async Task<int> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-s").ToArray() });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    return answer.RootElement.GetProperty("exitCode").GetInt32();
}
