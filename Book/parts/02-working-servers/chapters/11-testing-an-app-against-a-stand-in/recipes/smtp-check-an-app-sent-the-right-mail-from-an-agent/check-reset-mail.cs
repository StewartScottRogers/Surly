// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs the app's mail step against surl-mcp (the app's three sends go through curl-mcp's curl
// tool), waits for each delivery, and judges recipient, subject and link from read_message.
// Run: dotnet run check-reset-mail.cs
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// SMTP and IMAP on free ports over one mail store; each stored copy starts with Delivered-To.
var arguments = new List<string> { "--mail-delivered-to", "--mail-domain", "example.com" };
foreach (string name in new[] { "app", "ann", "bob", "carol", "dave" })
{
    arguments.AddRange(["--mailbox", $"{name}@example.com:{name}pass"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = arguments.ToArray(),
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;
string imap = server.GetProperty("bound_urls")[1].GetString()!;

// The app's three runs: the right mail to ann, a local link to carol, and To: dave sent to bob.
await Send("ann@example.com", "reset.eml");
await Send("carol@example.com", "local-link.eml");
await Send("bob@example.com", "wrong-to.eml");

// Recipient and subject: wait for the delivery into ann's mailbox, then read the stored copy.
string ann = await Delivered("ann@example.com", 10000) ?? "";
Console.WriteLine($"ann: {Fields(ann, "Delivered-To", "Subject")}");
Console.WriteLine($"ann's link: {Link(ann)}");
Console.WriteLine($"search in ann's INBOX: {await Search("ann@example.com")}");

// The local link: delivered, but the search for the right link finds nothing.
string carol = await Delivered("carol@example.com", 10000) ?? "";
Console.WriteLine($"search in carol's INBOX: {await Search("carol@example.com")}");
Console.WriteLine($"carol's link: {Link(carol)}");

// The wrong envelope: nothing reaches dave, so a two-second wait times out; bob has the copy.
Console.WriteLine($"dave: {(await Delivered("dave@example.com", 2000) is null ? "timed_out after 2000 ms" : "delivered")}");
string bob = await Delivered("bob@example.com", 10000) ?? "";
Console.WriteLine($"bob: {Fields(bob, "Delivered-To", "To")}");
return 0;

// The app's send, through the curl tool: the envelope recipient is the one that counts.
async Task Send(string recipient, string file) => await Curl(
    "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", recipient, "-T", file, smtp + "app.example.com");

// Waits, within the bound, for the first delivery to owner's mailbox; then reads that copy. Null if none came.
async Task<string?> Delivered(string owner, int timeoutMs)
{
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = owner, ["timeout_ms"] = timeoutMs,
    });
    if (!waited.StructuredContent!.Value.TryGetProperty("event", out _))
    {
        return null;
    }

    CallToolResult read = await surl.CallToolAsync("read_message", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["owner"] = owner, ["mailbox"] = "INBOX", ["uid"] = 1,
    });
    return read.StructuredContent!.Value.GetProperty("text").GetString();
}

// The named header fields, in the order the message has them.
static string Fields(string message, params string[] names) => string.Join(" | ",
    message[..message.IndexOf("\r\n\r\n")].Split("\r\n").Where(l => names.Any(n => l.StartsWith(n + ": "))));

// The first reset link in the message, whatever its scheme and host.
static string Link(string message) => Regex.Match(message, @"https?://\S+/reset/[0-9a-f]+").Value;

// Recipient, subject and link in one IMAP SEARCH, as the owner, through the curl tool.
async Task<string> Search(string owner)
{
    JsonElement answer = await Curl("-u", $"{owner}:{owner[..owner.IndexOf('@')]}pass",
        imap + $"INBOX?TO%20{owner}%20SUBJECT%20%22Reset%20your%20password%22%20BODY%20https://app.example.com/reset/");
    return answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim();
}

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
