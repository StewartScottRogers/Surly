// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent stands up a catch-all with surl-mcp, mails generated addresses with curl-mcp's curl tool
// as the app would, and counts the catch-all over POP3 and in the store.
// Run: dotnet run catch-all.cs -- <case>
//   catch-all     three generated addresses, one mail each (the default)
//   no-catch-all  the same, on a server with no catch-all
//   other-domain  one mail to an address outside the mail domain
//   no-domain     a catch-all with no --mail-domain
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "catch-all";

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

// SMTP and POP3 on free ports, example.com, a mailbox for the app and one for the catch-all.
List<string> options = which == "no-domain" ? [] : ["--mail-domain", "example.com",
    "--mailbox", "app@example.com:apppass", "--mailbox", "catchall@example.com:catchpass"];
if (which != "no-catch-all")
{
    options.AddRange(["--mail-catch-all", "example.com=catchall@example.com"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtp://127.0.0.1:0", "pop3://127.0.0.1:0" },
    ["arguments"] = options,
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    Console.WriteLine($"start_server: exit_code {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")}), {server.GetProperty("lines")[0]}");
    return 1;
}

string id = server.GetProperty("server_id").GetString()!;
string smtp = server.GetProperty("bound_urls")[0].GetString()!;
string pop3 = server.GetProperty("bound_urls")[1].GetString()!;

// The app: one mail to each address it made up.
string[] addresses = which == "other-domain" ? ["user-1@example.org"] : ["user-1@example.com", "user-2@example.com", "user-3@example.com"];
foreach (string address in addresses)
{
    JsonElement sent = await Curl("--crlf", "-u", "app@example.com:apppass", "--mail-from", "app@example.com",
        "--mail-rcpt", address, "-T", "invite.eml", smtp + "example.com");
    string error = sent.GetProperty("standardError").GetProperty("content").GetString()!.TrimEnd();
    Console.WriteLine($"sent to {address}: curl exit {sent.GetProperty("exitCode")}{(error == "" ? "" : $", {error}")}");
}

// The test: the catch-all's POP3 list, one line per message.
JsonElement listed = await Curl("-u", "catchall@example.com:catchpass", pop3);
string list = listed.GetProperty("standardOutput").GetProperty("content").GetString()!;
int caught = list.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length;
Console.WriteLine($"caught over POP3: {caught}");

// The same count from the store, with no client.
CallToolResult boxes = await surl.CallToolAsync("list_mailboxes", new Dictionary<string, object?> { ["server_id"] = id, ["owner"] = "catchall@example.com" });
Console.WriteLine($"list_mailboxes: {boxes.StructuredContent!.Value.GetProperty("mailboxes")[0].GetProperty("message_count")} messages");
return caught == addresses.Length ? 0 : 1;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s -S keeps only errors.
async Task<JsonElement> Curl(params string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments.Prepend("-S").Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
