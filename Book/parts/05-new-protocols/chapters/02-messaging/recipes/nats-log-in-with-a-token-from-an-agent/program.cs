// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent logs in to surl-mcp's NATS server with a token through the curl tool, and reads what the server recorded of
// each login, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   solution      a server that takes anonymous clients accepts a token unchecked (the default)
//   clear         a token over nats://, no loosening: refused
//   clear-events  what the server judged of that
//   loose         --allow-plaintext-auth, a wrong token: refused
//   loose-events  what the server judged of that
//   tls-wrong     natss://, a wrong token
//   tls-none      natss://, no token
//   untrusted     natss://, no trusted certificate
//   wrong-scheme  nats:// to a natss server
//   sent-as-token the record shows the token sent as auth_token, with no user and password
//   record        the record get_exchanges keeps holds what the client sent, token included
//   right         natss://, the token the server holds
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";
const string Token = "tok123";
bool tls = which is "tls-wrong" or "tls-none" or "untrusted" or "wrong-scheme" or "sent-as-token" or "record" or "right";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
string here = Directory.GetCurrentDirectory();
string cert = Path.Combine(here, "server.pem");
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

// The server: the token is an account with no name, --user :tok123.
var arguments = new List<string>();
if (which == "solution")
{
    arguments.Add("--allow-anonymous");
}
else
{
    arguments.AddRange(["--user", ":" + Token]);
}

if (which is "loose" or "loose-events")
{
    arguments.Add("--allow-plaintext-auth");
}

if (tls)
{
    arguments.AddRange(["--cert", cert, "--key", Path.Combine(here, "server.key")]);
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { tls ? "natss://127.0.0.1:0" : "nats://127.0.0.1:0" },
    ["arguments"] = arguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

switch (which)
{
    case "clear":
        await Publish(url, Token);
        break;
    case "clear-events":
        await Publish(url, Token);
        Console.WriteLine($"logins judged: {await Judged()}");
        break;
    case "loose":
        await Publish(url, "wrong");
        break;
    case "loose-events":
        await Publish(url, "wrong");
        Console.WriteLine($"logins judged: {await Judged()}");
        break;
    case "tls-wrong":
        await Publish(url, "wrong", "--cacert", cert, "--ssl-no-revoke");
        break;
    case "tls-none":
        await Publish(url, null, "--cacert", cert, "--ssl-no-revoke");
        break;
    case "untrusted":
        await Publish(url, Token);
        break;
    case "wrong-scheme":
        await Publish(url.Replace("natss://", "nats://"), Token, "--cacert", cert, "--ssl-no-revoke");
        break;
    case "sent-as-token":
        await Publish(url, "wrong", "--cacert", cert, "--ssl-no-revoke");
        string connect = Regex.Match(await Recorded(), "CONNECT [^\r\n]*").Value;
        Console.WriteLine($"auth_token: {Regex.Match(connect, "\"auth_token\":\"[^\"]*\"").Value}, user: {connect.Contains("\"user\"")}, pass: {connect.Contains("\"pass\"")}");
        break;
    case "record":
        await Publish(url, Token, "--cacert", cert, "--ssl-no-revoke");
        Console.WriteLine($"record holds the token: {(await Recorded()).Contains(Token)}");
        break;
    case "right":
        await Publish(url, Token, "--cacert", cert, "--ssl-no-revoke");
        break;
    default:
        await Publish(url, Token);
        break;
}

return 0;

// The curl tool runs curl's own arguments: -s keeps the progress meter out, -S puts an error message back, and a token is
// -u with the token as the user name and nothing after the colon. A refusal is an exit code and a message, not a tool error;
// a TLS error's wording is the TLS library's, so only its code is shown.
async Task Publish(string target, string? token, params string[] more)
{
    var all = new List<string> { "-s", "-S" };
    all.AddRange(more);
    if (token is not null)
    {
        all.AddRange(["-u", token + ":"]);
    }

    all.AddRange(["-d", "paid", target + "orders.new"]);
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = all.ToArray() });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    int exit = answer.GetProperty("exitCode").GetInt32();
    string error = answer.GetProperty("standardError").GetProperty("content").GetString()?.TrimEnd() ?? "";
    Console.WriteLine(exit == 0 ? "published" : exit == 60 ? $"refused: exit {exit}" : $"refused: exit {exit}, {error}");
}

// The server ends the client's exchange a moment after the client has gone, so the agent waits for that event, with a
// bound and not a sleep, before it reads the server's record.
async Task WaitForEnd()
{
    await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
    });
}

// The logins the server judged: login_judged events. A login refused unchecked, because of where it travelled, is not one.
async Task<int> Judged()
{
    await WaitForEnd();
    CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "login_judged" },
    });
    return events.StructuredContent!.Value.GetProperty("events").GetArrayLength();
}

// The server's record of every exchange, as text: what the client sent and what the server sent back.
async Task<string> Recorded()
{
    await WaitForEnd();
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
    var text = new System.Text.StringBuilder();
    foreach (JsonElement exchange in result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
    {
        foreach (JsonElement chunk in exchange.GetProperty("chunks").EnumerateArray())
        {
            text.Append(chunk.GetProperty("data").GetString());
        }
    }

    return text.ToString();
}
