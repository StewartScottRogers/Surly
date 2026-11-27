// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent watches a chat client's XMPP login: surl-mcp runs the XMPP server, curl-mcp's curl tool plays
// the client, and the agent reads the server's login_judged events to say which SASL mechanism the
// client tried, as which account, and why the login was refused.
// Run: dotnet run watch-login.cs -- <case>
//   wrong     the client's password is wrong (the default)
//   right     the right password
//   plain     the right password, on a server whose --auth accepts PLAIN alone
//   fault     the right password, with a refuse-login fault on the server
//   nologin   the client gives no user name or password
//   untrusted the right password, but the client does not trust the server's certificate
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "wrong";

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

// XMPP with STARTTLS on the certificate in this folder, and one account.
var arguments = new List<string> { "--cert", "server.pem", "--key", "server.key", "--user", "alice:s3cret" };
if (mode == "plain")
{
    arguments.AddRange(["--auth", "plain"]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "xmpp://127.0.0.1:0" }, ["arguments"] = arguments,
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string xmpp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
if (mode == "fault")
{
    await surl.CallToolAsync("add_fault", new Dictionary<string, object?> { ["server_id"] = id, ["fault"] = "refuse-login" });
}

// The chat client logs in and asks the server what it offers.
var curlArgs = new List<string> { "-s", "-S" };
if (mode != "untrusted")
{
    curlArgs.AddRange(["--cacert", "server.pem"]);
}

if (mode != "nologin")
{
    curlArgs.AddRange(["-u", mode == "wrong" ? "alice:s3cert" : "alice:s3cret"]);
}

curlArgs.Add(xmpp + "127.0.0.1");
CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
JsonElement client = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
string said = client.GetProperty("standardError").GetProperty("content").GetString()!.Split('\n')[0].TrimEnd();
Console.WriteLine($"client: exit {client.GetProperty("exitCode").GetInt32()}{(said == "" ? "" : ", " + said)}");

// The record is complete once the server has ended the exchange; then read the login events.
await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
});
CallToolResult got = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
});
JsonElement[] logins = got.StructuredContent!.Value.GetProperty("events").EnumerateArray().ToArray();
if (logins.Length == 0)
{
    Console.WriteLine("agent: no login reached the server's judge");
    return 1;
}

// The agent's reading of each login: mechanism, account, outcome, and whether a fault decided it.
foreach (JsonElement login in logins)
{
    string mechanism = login.GetProperty("mechanism").GetString()!, account = login.GetProperty("account").GetString()!;
    string outcome = login.GetProperty("is_accepted").GetBoolean() ? "accepted"
        : login.GetProperty("is_refused_by_fault").GetBoolean() ? "refused by a fault rule, not by the password"
        : "refused: the account and password did not match";
    Console.WriteLine($"agent: {account} tried {mechanism}; {outcome}");
}

return client.GetProperty("exitCode").GetInt32() == 0 ? 0 : 1;
