// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests an app's report mail: surl-mcp runs the mail server, curl-mcp's curl tool sends
// the mail as the app would and fetches the attachment's part over IMAP, and the agent decodes it
// and compares it with report.csv; surl-mcp's read_message shows what was stored.
// Run: dotnet run check-attachment.cs -- <case>
//   right          the app sends report-mail.eml, with report.csv attached (the default)
//   old-report     the app sends old-report-mail.eml, with last week's report
//   no-attachment  the app sends no-attachment.eml
#:package ModelContextProtocol.Core@2.2.0

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

// SMTP and IMAP on free ports over one mail store, serving example.com, with two mailboxes.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;
string imap = server.GetProperty("bound_urls")[1].GetString()!;

// The app's send: its login, the envelope, and the message from a file in this folder.
string file = which switch { "old-report" => "old-report-mail.eml", "no-attachment" => "no-attachment.eml", _ => "report-mail.eml" };
JsonElement sent = await Curl("--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com",
    "--mail-rcpt", "ann@example.com", "-T", file, smtp + "example.com");
Console.WriteLine($"app sent {file}: curl exit {sent.GetProperty("exitCode")}");

// What the store holds, read without a client: the attachment's header line, if there is one.
CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
{
    ["server_id"] = id, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX", ["uid"] = 1,
});
string stored = read.StructuredContent!.Value.GetProperty("text").GetString()!;
Console.WriteLine($"read_message: {stored.Split("\r\n").FirstOrDefault(line => line.StartsWith("Content-Disposition:")) ?? "no Content-Disposition"}");

// The test: part 2 of Ann's first message over IMAP, still base64, decoded and compared byte for byte.
JsonElement fetched = await Curl("-u", "ann@example.com:annpass", imap + "INBOX;UID=1;SECTION=2");
string part = fetched.GetProperty("standardOutput").GetProperty("content").GetString()!;
Console.WriteLine($"fetch part 2: curl exit {fetched.GetProperty("exitCode")}, {part.Length} characters");
if (part.Length == 0)
{
    Console.WriteLine("FAIL: no attachment");
    return 1;
}

byte[] attached = Convert.FromBase64String(part);
byte[] report = File.ReadAllBytes("report.csv");
bool same = attached.AsSpan().SequenceEqual(report);
Console.WriteLine(same ? $"PASS: attachment matches report.csv, {attached.Length} bytes" : $"FAIL: attachment is {attached.Length} bytes, report.csv {report.Length}");
return same ? 0 : 1;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
