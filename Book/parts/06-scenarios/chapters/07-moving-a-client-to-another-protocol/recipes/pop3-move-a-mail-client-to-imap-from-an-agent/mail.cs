// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent moves a mail client from POP3 to IMAP: surl-mcp serves one mailbox over both, curl-mcp reads
// it each way, and the agent reads the store and the server's login events back, over the official MCP SDK.
// Run: dotnet run mail.cs [-- ids|flags|password|delete|cutover]
//   (no argument) the first message over POP3 and over IMAP, compared, and the logins recorded
//   ids           the POP3 listing, the IMAP search and the POP3 unique IDs
//   flags         what each client's read does to the unseen count
//   password      a wrong password over both
//   delete        the old client deletes a message
//   cutover       a mailbox whose logins are limited to SMTP and IMAP
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "move";
const string Ann = "ann@example.com";

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

// surl-mcp: one mail store served over POP3 and IMAP, one mailbox, in surl's own words.
string mailbox = which == "cutover" ? $"{Ann}:s3cret;protocols=smtp,imap" : $"{Ann}:s3cret";
JsonElement started = await Call(surl, "start_server", new()
{
    ["listen_urls"] = new[] { "pop3://127.0.0.1:0", "imap://127.0.0.1:0" },
    ["arguments"] = new[] { "--mail-domain", "example.com", "--mailbox", mailbox },
});
string serverId = started.GetProperty("server_id").GetString()!;
string pop3 = started.GetProperty("bound_urls")[0].GetString()!;
string imap = started.GetProperty("bound_urls")[1].GetString()!;

// Two orders, delivered into the store as SMTP would.
for (int n = 1; n <= 2; n++)
{
    await Call(surl, "seed_mail", new() { ["server_id"] = serverId, ["to"] = Ann, ["message"] = $"From: shop@example.com\r\nTo: {Ann}\r\nSubject: Order {n}\r\n\r\norder {n}\r\n" });
}

switch (which)
{
    case "move":
        var overPop3 = await Curl(pop3 + "1");
        var overImap = await Curl(imap + "INBOX;UID=1");
        Console.WriteLine($"over pop3: {overPop3.Out.Length} characters");
        Console.WriteLine($"over imap: {overImap.Out.Length} characters");
        Console.WriteLine(overPop3.Out == overImap.Out ? "the two copies are identical" : "the copies DIFFER");
        foreach (string scheme in new[] { "pop3", "imap" })
        {
            JsonElement events = await Call(surl, "get_events", new() { ["server_id"] = serverId, ["kinds"] = new[] { "login_judged" }, ["scheme"] = scheme });
            int logins = events.GetProperty("events").EnumerateArray().Count(e => e.GetProperty("account").GetString() == Ann && e.GetProperty("is_accepted").GetBoolean());
            Console.WriteLine($"{scheme} logins recorded: {logins}");
        }

        break;
    case "ids":
        Console.Write((await Curl(pop3)).Out);
        Console.Write((await Curl(imap + "INBOX?ALL")).Out);
        Console.Write((await Curl("-X", "UIDL", pop3)).Out);
        break;
    case "flags":
        await Unseen("at start");
        await Curl(imap + "INBOX;UID=1");
        Console.Write($"IMAP search after an IMAP fetch of UID 1: {(await Curl(imap + "INBOX?UNSEEN")).Out}");
        await Curl(pop3 + "2");
        Console.Write($"IMAP search after a POP3 retrieve of 2: {(await Curl(imap + "INBOX?UNSEEN")).Out}");
        await Unseen("at the end");
        break;
    case "password":
        Console.WriteLine($"pop3: exit {(await Curl("-u", $"{Ann}:wrong", pop3)).Code}");
        Console.WriteLine($"imap: exit {(await Curl("-u", $"{Ann}:wrong", imap + "INBOX?ALL")).Code}");
        break;
    case "delete":
        await Curl("-X", "DELE 1", pop3);
        await Unseen("after a POP3 DELE 1");
        Console.WriteLine($"IMAP fetch of UID 1: exit {(await Curl(imap + "INBOX;UID=1")).Code}");
        break;
    case "cutover":
        Console.WriteLine($"pop3: exit {(await Curl(pop3)).Code}");
        Console.Write($"imap: {(await Curl(imap + "INBOX?ALL")).Out}");
        break;
}

return 0;

// One tool call; the structured answer, or the text answer parsed as JSON (curl-mcp answers in text).
async Task<JsonElement> Call(McpClient host, string tool, Dictionary<string, object?> arguments)
{
    CallToolResult result = await host.CallToolAsync(tool, arguments);
    return result.StructuredContent ?? JsonDocument.Parse(result.Content.OfType<TextContentBlock>().First().Text).RootElement;
}

// One curl-mcp call, logged in as ann unless the arguments say otherwise: the exit code and standard
// output, with CR LF line ends made LF. Each call is its own curl run, so a POP3 DELE commits at its end.
async Task<(int Code, string Out)> Curl(params string[] curlArguments)
{
    string[] login = curlArguments.Contains("-u") ? [] : ["-u", $"{Ann}:s3cret"];
    JsonElement answer = await Call(curl, "curl", new() { ["args"] = (string[])["-s", .. login, .. curlArguments] });
    return (answer.GetProperty("exitCode").GetInt32(), answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n"));
}

// The store's count for INBOX, from list_mailboxes, read without a protocol.
async Task Unseen(string when)
{
    JsonElement inbox = (await Call(surl, "list_mailboxes", new() { ["server_id"] = serverId, ["owner"] = Ann }))
        .GetProperty("mailboxes").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "INBOX");
    Console.WriteLine($"INBOX {when}: {inbox.GetProperty("message_count").GetInt32()} messages, {inbox.GetProperty("unseen_count").GetInt32()} unseen");
}
