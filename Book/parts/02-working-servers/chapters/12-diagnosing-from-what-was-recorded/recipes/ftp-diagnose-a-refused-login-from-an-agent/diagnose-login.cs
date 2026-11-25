// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent finds out why an FTP client's login failed with exit 67: surl-mcp runs the server, curl-mcp's
// curl tool plays the client, and the agent reads the login_judged events and, when there are none, the
// recorded exchange, to tell a wrong password from a password refused because it came in clear.
// server.pem and server.key are in the folder.
// Run: dotnet run diagnose-login.cs -- <case>
//   wrong    the client logs in over TLS with a wrong password
//   clear    the client sends the right password without TLS
//   clear-wrong  a wrong password without TLS
//   right    the right password over TLS
//   allowed  the clear case against a server started with --allow-plaintext-auth
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "wrong";

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

// FTP with a certificate, so a client can ask for TLS, one account, and a report to fetch.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "ftp://127.0.0.1:0" },
    ["arguments"] = (string[])["--cert", "server.pem", "--key", "server.key", "--user", "app:s3cret", .. which == "allowed" ? new[] { "--allow-plaintext-auth" } : []],
    ["files"] = new Dictionary<string, string> { ["out/report.csv"] = "region,total\r\nnorth,1200\r\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string ftp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The client's attempt.
string[] login = which switch
{
    "wrong" => ["--ssl-reqd", "--cacert", "server.pem", "-u", "app:s3cert"],
    "right" => ["--ssl-reqd", "--cacert", "server.pem", "-u", "app:s3cret"],
    "clear-wrong" => ["-u", "app:s3cert"],
    _ => ["-u", "app:s3cret"],
};
CallToolResult sent = await curl.CallToolAsync("curl", new Dictionary<string, object?>
{
    ["args"] = (string[])["-sS", .. login, "-o", "report.csv", ftp + "out/report.csv"],
});
using JsonDocument answer = JsonDocument.Parse(sent.Content.OfType<TextContentBlock>().Single().Text);
string said = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
Console.WriteLine($"curl exitCode {answer.RootElement.GetProperty("exitCode").GetInt32()}{(said.Length > 0 ? ", " + said : "")}");

// The record is complete once the server has ended the exchange; wait for that, with a bound.
CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "exchange_ended" }, ["scheme"] = "ftp", ["timeout_ms"] = 10000,
});
long exchange = ended.StructuredContent!.Value.GetProperty("event").GetProperty("exchange_id").GetInt64();

// The login events of that exchange.
CallToolResult judged = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" }, ["scheme"] = "ftp",
});
List<JsonElement> logins = judged.StructuredContent!.Value.GetProperty("events").EnumerateArray()
    .Where(e => e.GetProperty("exchange_id").GetInt64() == exchange).ToList();
foreach (JsonElement e in logins)
{
    Console.WriteLine($"login_judged: account {e.GetProperty("account")}, mechanism {e.GetProperty("mechanism")}, is_accepted {e.GetProperty("is_accepted")}");
}

// The exchange itself: what the client sent and what the server answered, a line at a time.
CallToolResult got = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
{
    ["server_id"] = id, ["after_exchange_id"] = exchange - 1, ["max_exchanges"] = 1,
});
var lines = new List<(string Who, string Line)>();
foreach (JsonElement chunk in got.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray())
{
    string who = chunk.GetProperty("direction").GetString() == "received" ? "C" : "S";
    string data = chunk.GetProperty("data").GetString()!;
    lines.AddRange((data.EndsWith("\r\n") ? data[..^2] : data).Split("\r\n").Select(l => (who, l)));
}

// The client's commands up to its password; the record keeps PASS as sent, so the agent withholds it.
List<string> commands = lines.Where(l => l.Who == "C").Select(l => l.Line).ToList();
int pass = commands.FindIndex(c => c.StartsWith("PASS "));
Console.WriteLine("login commands: " + string.Join(", ", commands.Take(pass).Append($"PASS ({commands[pass].Length - 5} characters, withheld)")));

// The agent's reading.
string refusal = lines.LastOrDefault(l => l.Who == "S" && l.Line.StartsWith("530")).Line ?? "";
if (logins.Any(e => e.GetProperty("is_accepted").GetBoolean()))
{
    Console.WriteLine("diagnosis: the login was accepted");
}
else if (logins.Count > 0)
{
    Console.WriteLine($"diagnosis: wrong password; the server checked it and refused it ({refusal})");
}
else
{
    bool tls = commands.Any(c => c.StartsWith("AUTH "));
    Console.WriteLine($"diagnosis: no login was judged; the client {(tls ? "asked" : "never asked")} for TLS and the server refused the password unchecked ({refusal})");
}

return 0;
