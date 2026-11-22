// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a mail server for example.com with surl-mcp that refuses every other recipient,
// sends as the app with curl-mcp's curl tool, and reads what the server answered and stored.
// Run: dotnet run stray-recipient.cs -- <case>
//   stray       the app mails carol@elsewhere.org (the default)
//   own-domain  the app mails ann@example.com
//   seed        the agent seeds mail for carol@elsewhere.org itself
//   no-domain   the app mails carol@elsewhere.org, and the server has no mail domain
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "stray";

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

// SMTP on a free port: mail for example.com only, with the app's mailbox and ann's.
string[] options = which == "no-domain"
    ? ["--user", "app@example.com:apppass"]
    : ["--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0" },
    ["arguments"] = options,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

if (which == "seed")
{
    // seed_mail delivers as SMTP would, so the mail domain applies to it too.
    CallToolResult seeded = await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["to"] = "carol@elsewhere.org", ["message"] = "Subject: Welcome\r\n\r\nThanks for signing up.\r\n",
    });
    Console.WriteLine($"seed_mail: tool error {(seeded.IsError == true ? "yes" : "no")}, {seeded.Content.OfType<TextContentBlock>().Single().Text}");
    return seeded.IsError == true ? 1 : 0;
}

// The app: logged in, sending welcome.eml from this folder to the recipient the case chooses.
string to = which == "own-domain" ? "ann@example.com" : "carol@elsewhere.org";
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = new[] { "-s", "-S", "--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", to, "-T", "welcome.eml", smtp + "example.com" },
});
using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
string error = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
Console.WriteLine($"curl to {to}: exit {answer.RootElement.GetProperty("exitCode")}, tool error {(result.IsError == true ? "yes" : "no")}{(error == "" ? "" : $", {error}")}");

// What the server answered RCPT TO, byte for byte as recorded. The record of the exchange can lag
// curl's answer a moment, so the agent reads it again until it holds the reply, for at most 10 s.
JsonElement[] chunks = await Chunks();
int rcpt = Array.FindIndex(chunks, c => c.GetProperty("data").GetString()!.StartsWith("RCPT TO:"));
DateTime deadline = DateTime.UtcNow.AddSeconds(10);
while ((rcpt < 0 || rcpt + 1 >= chunks.Length) && DateTime.UtcNow < deadline)
{
    await Task.Delay(100);
    chunks = await Chunks();
    rcpt = Array.FindIndex(chunks, c => c.GetProperty("data").GetString()!.StartsWith("RCPT TO:"));
}

Console.WriteLine($"get_exchanges: {chunks[rcpt].GetProperty("data").GetString()!.TrimEnd()} -> {chunks[rcpt + 1].GetProperty("data").GetString()!.TrimEnd()}");

// What the server stored for ann, read with no client.
if (which != "no-domain")
{
    CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "ann@example.com" });
    Console.WriteLine($"list_mailboxes ann@example.com: message_count {boxes.StructuredContent!.Value.GetProperty("mailboxes")[0].GetProperty("message_count")}");
}

return result.IsError == true ? 1 : 0;

// The chunks of the server's first SMTP exchange, or none while it has not recorded one.
async Task<JsonElement[]> Chunks()
{
    CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = "smtp" });
    JsonElement all = exchanges.StructuredContent!.Value.GetProperty("exchanges");
    return all.GetArrayLength() == 0 ? [] : [.. all[0].GetProperty("chunks").EnumerateArray().Select(c => c.Clone())];
}
