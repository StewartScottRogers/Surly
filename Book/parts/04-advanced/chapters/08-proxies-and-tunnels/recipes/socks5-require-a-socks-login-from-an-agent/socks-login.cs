// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an origin and a SOCKS5 server with one account through surl-mcp, logs in through it
// with curl-mcp, and reads back from surl-mcp how the server judged the login.
// Run: dotnet run socks-login.cs -- <server> <login>
//   server  open (started with --allow-plaintext-auth, the default) or strict (as surl starts by default)
//   login   right (the default), none, wrong, unknown (a user with no account),
//           or field (the right login in transfer's proxyUser field, not in the proxy URL)
#:package ModelContextProtocol.Core@*

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "open";
string login = args.Length > 1 ? args[1] : "right";

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

// One server: the origin on localhost and a SOCKS5 server; -U gives every proxy listener an account.
List<string> arguments = ["-U", "app:socks-pw"];
if (mode == "open") arguments.Add("--allow-plaintext-auth");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://localhost:0", "socks5h://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string id = server.GetProperty("server_id").GetString()!;
string origin = server.GetProperty("bound_urls")[0].GetString()!;
string socks = server.GetProperty("bound_urls")[1].GetString()!;
foreach (JsonElement line in server.GetProperty("log_lines").EnumerateArray().Where(l => l.GetString()!.Contains("warning")))
    Console.WriteLine($"log_lines: {line.GetString()}");

// curl-mcp transfer: a login in the proxy URL as user:password@, or in the proxyUser field.
string proxy = login switch
{
    "right" => socks.Replace("socks5h://", "socks5h://app:socks-pw@"),
    "wrong" => socks.Replace("socks5h://", "socks5h://app:wrong-pw@"),
    "unknown" => socks.Replace("socks5h://", "socks5h://bob:socks-pw@"),
    _ => socks,
};
var fields = new Dictionary<string, object?> { ["url"] = origin + "hello.txt", ["proxy"] = proxy, ["timeoutSeconds"] = 20 };
if (login == "field") fields["proxyUser"] = "app:socks-pw";
CallToolResult fetched = await curl.CallToolAsync("transfer", fields);
JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
JsonElement failure = answer.GetProperty("failure");
Console.WriteLine(failure.ValueKind == JsonValueKind.Null
    ? $"transfer: statusCode {answer.GetProperty("statusCode")}, {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd()}"
    : $"transfer: exitCode {failure.GetProperty("exitCode")}, {failure.GetProperty("text").GetString()!.Split('\n')[0]}, tool error: {(fetched.IsError == true ? "yes" : "no")}");

// surl-mcp get_events: the login the server judged, if it judged one.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" }, ["listen_url"] = socks,
});
JsonElement judged = events.StructuredContent!.Value.GetProperty("events");
if (judged.GetArrayLength() == 0) Console.WriteLine("login_judged: none");
foreach (JsonElement e in judged.EnumerateArray())
    Console.WriteLine($"login_judged: account {e.GetProperty("account")}, mechanism {e.GetProperty("mechanism")}, is_accepted {e.GetProperty("is_accepted").GetBoolean().ToString().ToLowerInvariant()}");

// surl-mcp get_exchanges: the SOCKS5 server's replies, in hexadecimal, and whether the password is in
// what the client sent, as the server recorded it.
CallToolResult exchanges = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
{
    ["server_id"] = id, ["listen_url"] = socks,
});
foreach (JsonElement exchange in exchanges.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray())
{
    var chunks = exchange.GetProperty("chunks").EnumerateArray()
        .Where(c => c.GetProperty("encoding").GetString() == "base64")
        .Select(c => (Sent: c.GetProperty("direction").GetString() == "sent", Bytes: Convert.FromBase64String(c.GetProperty("data").GetString()!)))
        .ToList();
    Console.WriteLine($"replies: {string.Join(' ', chunks.Where(c => c.Sent).Select(c => Convert.ToHexString(c.Bytes)))}");
    bool password = chunks.Any(c => !c.Sent && Encoding.ASCII.GetString(c.Bytes).Contains("socks-pw"));
    Console.WriteLine($"socks-pw in the record: {(password ? "yes" : "no")}");
}
return 0;
