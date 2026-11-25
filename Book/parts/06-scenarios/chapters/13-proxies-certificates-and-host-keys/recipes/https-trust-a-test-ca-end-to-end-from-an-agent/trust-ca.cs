// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs HTTPS and SMTPS servers whose certificates one test certificate authority signed,
// with surl-mcp, and reaches them with curl-mcp's curl tool trusting that authority alone. The
// certificates come from setup: in recipe.yml.
// Run: dotnet run trust-ca.cs [-- <case>]
//   (no argument)  trust ca.pem: fetch over HTTPS, send over SMTPS, find the mail in Ann's mailbox
//   no-ca          trust nothing: both calls fail
//   stray          trust ca.pem, against a server another authority signed for
//   insecure       skip the check (-k) against that server
//   revocation     trust ca.pem, without --ssl-no-revoke
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string mode = args.Length > 0 ? args[0] : "trusted";

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

// HTTPS with the www certificate, or with the one another authority signed.
string web = mode is "stray" or "insecure" ? "stray" : "www";
CallToolResult webStarted = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "https://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", $"{web}.pem", "--key", $"{web}.key" },
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
string https = webStarted.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// SMTPS with the mail certificate, for example.com: the app's mailbox and Ann's.
CallToolResult mailStarted = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "smtps://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "mail.pem", "--key", "mail.key", "--mail-domain", "example.com", "--mailbox", "app@example.com:apppass", "--mailbox", "ann@example.com:annpass" },
});
string mailId = mailStarted.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string smtps = mailStarted.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The trust every call carries: the test CA alone, and, for Windows, no revocation question.
var trust = new List<string>();
if (mode != "no-ca" && mode != "insecure") trust.AddRange(["--cacert", "ca.pem"]);
if (mode != "revocation") trust.Add("--ssl-no-revoke");
if (mode == "insecure") trust.Add("-k");

(int exit, string output) = await Curl([.. trust, "-s", https + "hello.txt"]);
Console.WriteLine($"HTTPS: curl exit {exit}{(exit == 0 ? ", " + output.TrimEnd() : "")}");
if (mode is "stray" or "insecure" or "revocation")
{
    return 0;
}

(exit, output) = await Curl([.. trust, "-s", "-u", "app@example.com:apppass", "--mail-from", "app@example.com", "--mail-rcpt", "ann@example.com",
    "-T", "note.eml", "-w", "%{response_code}", smtps + "client.example.com"]);
Console.WriteLine($"SMTPS: curl exit {exit}{(exit == 0 ? ", reply " + output : "")}");
if (exit != 0)
{
    return 0;
}

// Delivery is asynchronous: wait, with a bound, for the mail to reach Ann's mailbox, then count it.
await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
{
    ["server_id"] = mailId, ["kinds"] = new[] { "store_changed" }, ["store"] = "mail", ["name"] = "ann@example.com", ["timeout_ms"] = 10000,
});
CallToolResult listed = await surl.CallToolAsync("list_messages", new Dictionary<string, object?>
{
    ["server_id"] = mailId, ["owner"] = "ann@example.com", ["mailbox"] = "INBOX",
});
Console.WriteLine($"ann@example.com holds {listed.StructuredContent!.Value.GetProperty("messages").GetArrayLength()}");
return 0;

// The curl tool runs curl's own arguments and answers exitCode and standardOutput as JSON; a
// nonzero exit is a tool error with the same answer.
async Task<(int, string)> Curl(string[] arguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement stdout = answer.RootElement.GetProperty("standardOutput");
    string text = stdout.ValueKind == JsonValueKind.Object ? stdout.GetProperty("content").GetString() ?? "" : "";
    return (answer.RootElement.GetProperty("exitCode").GetInt32(), text);
}
