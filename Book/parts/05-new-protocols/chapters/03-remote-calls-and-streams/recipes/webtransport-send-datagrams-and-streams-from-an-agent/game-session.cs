// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a WebTransport server that needs a login (surl-mcp) and sends it datagrams and
// streams (curl-mcp), over the official MCP SDK.
// Run: dotnet run game-session.cs -- <case>
//   datagram    the game state as one datagram (the default)
//   uni         a log line on a one-way stream          bidi       data on a two-way stream
//   big-stream  100,000 bytes on a one-way stream
//   fits        a 1,099-byte datagram                   too-big    a 1,100-byte datagram
//   none        no login                                wrong      a wrong password, and surl's record of it
//   no-cert     start webtransport with no certificate
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "datagram";

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

// surl-mcp: WebTransport on a free UDP port, with the certificate and one account.
string[] arguments = which == "no-cert"
    ? ["--user", "player1:secret"]
    : ["--cert", "server.pem", "--key", "server.key", "--user", "player1:secret"];
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "webtransport://127.0.0.1:0" }, ["arguments"] = arguments,
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    Console.WriteLine($"exit_code: {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")})");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    return 1;
}

string url = server.GetProperty("bound_urls")[0].GetString()! + "game";

// curl-mcp: one curl command line per case, bounded at 10 seconds with -m.
string? stdin = null;
var words = new List<string> { "-sS", "-m", "10", "--cacert", "server.pem" };
words.AddRange(which switch
{
    "none" => [],
    "wrong" => ["-u", "player1:guess"],
    _ => (string[])["-u", "player1:secret"],
});
switch (which)
{
    case "uni": words.AddRange(["-d", "level 2 loaded", url + "?wt=uni"]); break;
    case "bidi": words.AddRange(["-d", "ping", url]); break;
    case "big-stream" or "fits" or "too-big":
        // The payload comes in as curl's standard input; --data-binary @- sends it as it is.
        stdin = new string('x', which switch { "big-stream" => 100_000, "fits" => 1099, _ => 1100 });
        words.AddRange(["--data-binary", "@-", url + (which == "big-stream" ? "?wt=uni" : "?wt=datagram")]);
        break;
    default: words.AddRange(["-d", """{"player":1,"x":3,"y":4}""", url + "?wt=datagram"]); break;
}

CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words, ["stdin"] = stdin });
JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
JsonElement output = answer.GetProperty("standardOutput");
Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
if (stdin is null)
{
    Console.WriteLine($"standardOutput: {output.GetProperty("content").GetString()}");
}
else
{
    Console.WriteLine($"sent {stdin.Length} bytes, echoed {output.GetProperty("totalBytes")}");
}

Console.Write(answer.GetProperty("standardError").GetProperty("content").GetString());

if (which == "wrong")
{
    // surl-mcp: the login surl judged for the session, by account.
    CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server.GetProperty("server_id").GetString(), ["kinds"] = new[] { "login_judged" },
    });
    JsonElement login = waited.StructuredContent!.Value.GetProperty("event");
    Console.WriteLine($"login_judged: {login.GetProperty("account")}, accepted {login.GetProperty("is_accepted")}");
}

Console.WriteLine($"tool error: {(ran.IsError == true ? "yes" : "no")}");
return ran.IsError == true ? 1 : 0;
