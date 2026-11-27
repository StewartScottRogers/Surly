// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a mail login with an OAuth 2.0 token: surl-mcp runs the IMAP server that holds the token as an
// account, curl-mcp's curl tool is the client, and surl-mcp's events show what the server judged.
// Run: dotnet run oauth-token.cs [-- <case>]
//   (no argument)  the token over imaps and over plain imap, then what the server judged
//   loose          the same token over plain imap on a server started with --allow-plaintext-auth
//   one            a server started with --auth oauthbearer, and a client that insists on XOAUTH2
// server.pem and server.key, a certificate and key for 127.0.0.1, are in this folder.
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "default";

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

// imaps and imap over one mail store. The token is an account with an empty user name (--user :token).
var arguments = new List<string> { "--cert", "server.pem", "--key", "server.key", "--user", ":tok-abc123", "--mail-domain", "example.com", "--mailbox", "ann@example.com:annpass", "--mailbox", "bob@example.com:bobpass" };
if (which == "loose")
{
    arguments.Add("--allow-plaintext-auth");
}

if (which == "one")
{
    arguments.AddRange(["--auth", "oauthbearer"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "imaps://127.0.0.1:0", "imap://127.0.0.1:0" }, ["arguments"] = arguments.ToArray(),
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string imaps = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string imap = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()!;

// One message in ann's INBOX, and a bounded wait for the delivery event before anyone reads it.
await surl.CallToolAsync("seed_mail", new Dictionary<string, object?>
{
    ["server_id"] = id, ["to"] = "ann@example.com",
    ["message"] = "From: ann@example.com\r\nTo: ann@example.com\r\nSubject: Lunch\r\n\r\nNoon at the usual place.\r\n",
});
CallToolResult wait = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["timeout_ms"] = 10000,
});
if (wait.StructuredContent!.Value.TryGetProperty("timed_out", out _))
{
    Console.WriteLine("no delivery event within 10 s");
    return 1;
}

const string Message = "INBOX;UID=1;SECTION=TEXT";

// One curl run with its own login: the exit code, and the body when it worked.
async Task<string> Fetch(string label, string url, string user, string token, params string[] more)
{
    JsonElement result = await Curl(["--cacert", "server.pem", "-u", user, "--oauth2-bearer", token, .. more, url]);
    int exit = result.GetProperty("exitCode").GetInt32();
    return exit == 0 ? $"{label}: curl exit 0, {result.GetProperty("standardOutput").GetProperty("content").GetString()!.Trim()}" : $"{label}: curl exit {exit}";
}

// Raw IMAP: AUTHENTICATE XOAUTH2 written to the port by curl's telnet:// from a fixture; prints the tagged reply.
async Task<string> Raw(string label, string file, string url)
{
    JsonElement result = await Curl("-T", file, url.Replace("imap://", "telnet://"));
    string replies = result.GetProperty("standardOutput").GetProperty("content").GetString()!;
    return $"{label}: {replies.Split("\r\n").First(line => line.StartsWith("a1 "))}";
}

// The mechanisms a listener advertised: the first line the server sent in the first exchange it recorded.
async Task<string> Advertised(string scheme)
{
    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id, ["scheme"] = scheme, ["max_exchanges"] = 1 });
    string greeting = recorded.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray()
        .First(c => c.GetProperty("direction").GetString() == "sent").GetProperty("data").GetString()!.Split("\r\n")[0];
    return string.Join(" ", greeting.Split(' ', ']').Where(word => word.StartsWith("AUTH=")));
}

switch (which)
{
    case "loose":
        Console.WriteLine(await Fetch("imap, token", imap + Message, "ann@example.com", "tok-abc123"));
        Console.WriteLine(await Fetch("imap, wrong token", imap + Message, "ann@example.com", "wrong-token"));
        Console.WriteLine(await Raw("imap, raw XOAUTH2, right token", "right.imap", imap));
        break;
    case "one":
        Console.WriteLine(await Fetch("imaps, default mechanism", imaps + Message, "ann@example.com", "tok-abc123"));
        Console.WriteLine(await Fetch("imaps, XOAUTH2 insisted on", imaps + Message, "ann@example.com", "tok-abc123", "--login-options", "AUTH=XOAUTH2"));
        Console.WriteLine($"imaps advertises: {await Advertised("imaps")}");
        break;
    default:
        Console.WriteLine(await Fetch("imaps, token", imaps + Message, "ann@example.com", "tok-abc123"));
        Console.WriteLine(await Fetch("imaps, XOAUTH2", imaps + Message, "ann@example.com", "tok-abc123", "--login-options", "AUTH=XOAUTH2"));
        Console.WriteLine(await Fetch("imaps, wrong token", imaps + Message, "ann@example.com", "wrong-token"));
        Console.WriteLine(await Fetch("imaps, bob with the token", imaps + Message, "bob@example.com", "tok-abc123"));
        Console.WriteLine(await Fetch("imap, token", imap + Message, "ann@example.com", "tok-abc123"));
        Console.WriteLine(await Raw("imap, raw XOAUTH2, right token", "right.imap", imap));
        Console.WriteLine(await Raw("imap, raw XOAUTH2, wrong token", "wrong.imap", imap));
        Console.WriteLine(await Fetch("imap, token after STARTTLS", imap + Message, "ann@example.com", "tok-abc123", "--ssl-reqd"));
        Console.WriteLine($"imaps advertises: {await Advertised("imaps")}");
        Console.WriteLine($"imap advertises: {await Advertised("imap")}");
        break;
}

// What the server judged, in order and by scheme: one login_judged event per login it decided.
CallToolResult logins = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "login_judged" } });
foreach (JsonElement login in logins.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"login_judged: {login.GetProperty("scheme").GetString()} {login.GetProperty("mechanism").GetString()} {login.GetProperty("account").GetString()} is_accepted {login.GetProperty("is_accepted").GetRawText()}");
}

return 0;

// The curl tool runs curl's own arguments and answers in text that is JSON; -s keeps the progress meter out.
async Task<JsonElement> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments.Prepend("-s").ToArray() });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();
}
