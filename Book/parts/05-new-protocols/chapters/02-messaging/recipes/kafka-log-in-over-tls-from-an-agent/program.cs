// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent starts a Kafka broker with one account over TLS or without it using surl-mcp, logs in with curl-mcp's curl
// tool, and reads the broker's log. Run: dotnet run program.cs -- <case>
//   tls            kafkas, the right password, the certificate trusted
//   tls-wrong      kafkas, a wrong password
//   tls-no-login   kafkas, no login
//   tls-untrusted  kafkas, the right password, the certificate not trusted
//   plain          kafka, the right password
//   plain-wrong    kafka, a wrong password
//   scram-256      kafka offering SCRAM-SHA-256 only, a wrong password
//   only-plain     kafka told to offer PLAIN only, the right password
//   loosened       kafka with --allow-plaintext-auth, a wrong password
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args[0];
bool tls = which.StartsWith("tls");

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. It holds server.pem and
// server.key, which openssl wrote.
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

// surl-mcp: one account and no loosening option unless the case names it; --auth replaces what the listener offers.
var serverArguments = new List<string> { "--user", "alice:s3cret" };
if (tls) serverArguments.AddRange(["--cert", "server.pem", "--key", "server.key"]);
if (which == "scram-256") serverArguments.AddRange(["--auth", "SCRAM-SHA-256"]);
if (which == "only-plain") serverArguments.AddRange(["--auth", "PLAIN"]);
if (which == "loosened") serverArguments.Add("--allow-plaintext-auth");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { tls ? "kafkas://127.0.0.1:0" : "kafka://127.0.0.1:0" },
    ["arguments"] = serverArguments,
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "orders";

// curl-mcp's curl tool takes curl's arguments, one list item each: -u is the login, --cacert the certificate to trust.
var login = new List<string>();
if (tls && which != "tls-untrusted") login.AddRange(["--cacert", "server.pem"]);
if (which != "tls-no-login")
{
    bool wrong = which is "tls-wrong" or "plain-wrong" or "scram-256" or "loosened";
    login.AddRange(["-u", wrong ? "alice:wrong" : "alice:s3cret"]);
}

bool accepted = which is "tls" or "plain";
if (accepted)
{
    await Curl("produce", [.. login, "-d", "first", url]);
    await Curl("consume", [.. login, url]);

    // surl-mcp: the broker's own record of what the login let through.
    CallToolResult log = await surl.CallToolAsync("read_log", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["topic"] = "orders", ["from_offset"] = 0, ["limit"] = 10,
    });
    JsonElement records = log.StructuredContent!.Value.GetProperty("records");
    Console.WriteLine($"read_log orders: {string.Join(", ", records.EnumerateArray().Select(r => $"{r.GetProperty("offset")}={r.GetProperty("text").GetString()}"))}");
}
else
{
    await Curl("consume", [.. login, url]);
}

return 0;

// A refusal is a result of the tool, not a failure of the call: exitCode and the text of standardError. A login
// refusal's text is the broker's and is printed; a failed handshake's is the TLS library's and differs by platform.
async Task Curl(string what, string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS" }.Concat(curlArguments).ToArray(),
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    int exit = answer.GetProperty("exitCode").GetInt32();
    string output = Text(answer.GetProperty("standardOutput"));
    string error = Text(answer.GetProperty("standardError"));
    string said = exit == 67 ? $" {error}" : output == "" ? "" : $", {output}";
    Console.WriteLine($"curl {what}: exit {exit}{said}");
}

// A stream with nothing in it may be null, not an empty object.
static string Text(JsonElement stream) => stream.ValueKind == JsonValueKind.Null ? "" : stream.GetProperty("content").GetString()!.Trim();
