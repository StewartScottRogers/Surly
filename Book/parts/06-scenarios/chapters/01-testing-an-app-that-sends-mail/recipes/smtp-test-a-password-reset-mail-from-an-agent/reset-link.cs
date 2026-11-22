// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests an app's password reset mail: surl-mcp serves SMTP, IMAP and HTTP, the app publishes
// its reset page with put_file and mails the link through curl-mcp's curl tool, and the test reads the
// mail over IMAP, pulls out the link and fetches it with curl-mcp's transfer tool.
// Run: dotnet run reset-link.cs -- <case>
//   right        the app mails a link to the page it published (the default)
//   wrong-token  the app's link names a token it never published
//   no-link      the app's mail has no link in it
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
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

// SMTP, IMAP and HTTP on free ports over one server's stores, serving mail for example.com.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;
string imap = server.GetProperty("bound_urls")[1].GetString()!;
string http = server.GetProperty("bound_urls")[2].GetString()!;

// The app: a fresh token, its reset page published under it, and a mail with the link.
string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
await surl.CallToolAsync("put_file", new Dictionary<string, object?>
{
    ["server_id"] = id, ["path"] = $"/reset/{token}", ["text"] = "Choose a new password for ann@example.com.\n",
});
string link = which switch { "wrong-token" => $"{http}reset/0000000000000000", "no-link" => "", _ => $"{http}reset/{token}" };
File.WriteAllText("reset.eml", $"From: app@example.com\r\nTo: ann@example.com\r\nSubject: Reset your password\r\n\r\nOpen this link to choose a new password:\r\n{link}\r\n");
JsonElement sent = await Curl("-u", "app@example.com:apppass", "--mail-from", "app@example.com",
    "--mail-rcpt", "ann@example.com", "-T", "reset.eml", smtp + "example.com");
Console.WriteLine($"app sent: curl exit {sent.GetProperty("exitCode")}");

// The test: the body of Ann's first message over IMAP, and the first reset link in it.
JsonElement fetched = await Curl("-u", "ann@example.com:annpass", imap + "INBOX;UID=1;SECTION=TEXT");
string body = fetched.GetProperty("standardOutput").GetProperty("content").GetString()!;
Match found = Regex.Match(body, @"http://\S+/reset/\S+");
if (!found.Success)
{
    Console.WriteLine("FAIL: no reset link in the mail");
    return 1;
}

Console.WriteLine($"link: {found.Value}");

// Following it with transfer, with no login, as the person resetting their password would.
CallToolResult followed = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = found.Value });
JsonElement answer = JsonDocument.Parse(followed.Content.OfType<TextContentBlock>().Single().Text).RootElement;
int status = answer.GetProperty("statusCode").GetInt32();
if (status != 200)
{
    Console.WriteLine($"FAIL: the link answered {status}");
    return 1;
}

Console.Write($"{status}: {answer.GetProperty("body").GetProperty("content").GetString()}");
return 0;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
