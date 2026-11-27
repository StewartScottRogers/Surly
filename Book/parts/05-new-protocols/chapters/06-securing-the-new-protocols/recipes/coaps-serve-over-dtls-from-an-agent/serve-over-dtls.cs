// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves a sensor's configuration over CoAP inside DTLS (surl-mcp) and reads it (curl-mcp),
// over the official MCP SDK.
// Run: dotnet run serve-over-dtls.cs -- <case>
//   transfer      coaps with the certificate; read it with the transfer tool (the default)
//   code          the same server; read it with curl -i for the response code
//   untrusted     the same, the certificate not trusted     plain-port   plain coap to the coaps port
//   account       an account: plain coap with curl -i       account-transfer  the same with transfer
//   password      the same with -u                          account-dtls an account: coaps with curl -i
//   anonymous     an account and --allow-anonymous          psk          an account and no certificate
//   nothing       coaps with no certificate and no account
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "transfer";
bool plain = which is "account" or "account-transfer" or "password" or "anonymous";

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

// surl-mcp: one resource on a free UDP port; the certificate, an account, or both, as the case says.
var arguments = new List<string>();
if (!plain && which is not ("psk" or "nothing")) arguments.AddRange(["--cert", "server.pem", "--key", "server.key"]);
if (which is "account" or "account-transfer" or "password" or "account-dtls" or "anonymous" or "psk") arguments.AddRange(["--user", "alice:secret"]);
if (which == "anonymous") arguments.Add("--allow-anonymous");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { plain ? "coap://127.0.0.1:0" : "coaps://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["sensor.json"] = "{\"interval\":60}\n" },
});
JsonElement server = started.StructuredContent!.Value;
if (started.IsError == true)
{
    Console.WriteLine($"exit_code: {server.GetProperty("exit_code")} ({server.GetProperty("exit_code_name")})");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    return 1;
}

string url = server.GetProperty("bound_urls")[0].GetString()! + "sensor.json";
if (which == "plain-port") url = url.Replace("coaps://", "coap://");

CallToolResult result;
if (which is "transfer" or "account-transfer")
{
    // transfer: the request as fields; caFile trusts the certificate, timeoutSeconds bounds it.
    var fields = new Dictionary<string, object?> { ["url"] = url, ["timeoutSeconds"] = 10 };
    if (!plain) fields["caFile"] = "server.pem";
    result = await curl.CallToolAsync("transfer", fields);
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"statusCode: {answer.GetProperty("statusCode")}, failure: {answer.GetProperty("failure").GetRawText()}");
    Console.WriteLine($"body: {answer.GetProperty("body").GetProperty("content").GetString()!.TrimEnd('\n')}");
}
else
{
    // curl: -i writes the CoAP response code before the payload; -m 10 bounds the request.
    var words = new List<string> { "-sS", "-i", "-m", "10" };
    if (which != "untrusted" && !plain) words.AddRange(["--cacert", "server.pem"]);
    if (which == "password") words.AddRange(["-u", "alice:secret"]);
    words.Add(url);
    result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    Console.WriteLine($"exitCode: {answer.GetProperty("exitCode")}");
    Console.Write(answer.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r\n", "\n"));
}

Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
