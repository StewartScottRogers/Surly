// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent logs in to AMQP over TLS and plain AMQP: surl-mcp starts the broker, curl-mcp logs in, over the official
// MCP SDK. Run: dotnet run program.cs -- <case>
//   login                     alice publishes and reads a message over AMQPS, trusting the certificate
//   plain-refused             the same login over plain AMQP on the same server
//   plaintext-allowed         plain AMQP on a server started with --allow-plaintext-auth
//   wrong-password            a wrong password over AMQPS
//   unknown-user              a user name with no account over AMQPS
//   no-login                  no login at all over AMQPS
//   plaintext-wrong-password  a wrong password over plain AMQP with --allow-plaintext-auth
//   untrusted-certificate     AMQPS with no certificate to trust
//   needs-a-certificate       start an amqps listener with no certificate
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "login";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it, and it holds the certificate.
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

// One server, two listeners: AMQPS with the certificate setup made, and plain AMQP. One account and, unless a case
// asks for it, no loosening. The options are surl's own words, in arguments.
var arguments = new List<string> { "--user", "alice:s3cret" };
if (which != "needs-a-certificate")
{
    arguments.AddRange(["--cert", "server.pem", "--key", "server.key"]);
}

if (which is "plaintext-allowed" or "plaintext-wrong-password")
{
    arguments.Add("--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "amqps://127.0.0.1:0", "amqp://127.0.0.1:0" },
    ["arguments"] = arguments.ToArray(),
});
if (started.IsError == true)
{
    // A listener of that scheme with no certificate does not start: surl's own exit code, in structured content.
    JsonElement refusal = started.StructuredContent!.Value;
    Console.WriteLine($"exit_code: {refusal.GetProperty("exit_code")} ({refusal.GetProperty("exit_code_name")})");
    Console.WriteLine("tool error: yes");
    return 1;
}

string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string amqps = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "%2F/orders";   // %2F is the virtual host /
string amqp = started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()! + "%2F/orders";
CallToolResult last = started;

switch (which)
{
    case "plain-refused" or "plaintext-allowed":
        await Login(amqp, "alice:s3cret");
        break;
    case "plaintext-wrong-password":
        await Login(amqp, "alice:wrong");
        break;
    case "wrong-password":
        await Login(amqps, "alice:wrong", trust: true);
        break;
    case "unknown-user":
        await Login(amqps, "bob:s3cret", trust: true);
        break;
    case "no-login":
        await Login(amqps, null, trust: true);
        break;
    case "untrusted-certificate":
        await Login(amqps, "alice:s3cret");
        break;
    default:
        await Login(amqps, "alice:s3cret", trust: true);
        break;
}

// The exit code says whether the last call was a tool error: curl's non-zero exit is one.
Console.WriteLine($"tool error: {(last.IsError == true ? "yes" : "no")}");
return last.IsError == true ? 1 : 0;

// Publish paid, then read it back, as curl would with -d and without. --cacert makes curl trust the certificate. A
// login that is refused stops at the publish, and the program prints curl's exit code and, for a login refusal,
// surl's words; a certificate refusal is the TLS library's, so the code alone.
async Task Login(string url, string? login, bool trust = false)
{
    var common = new List<string> { "-sS", "-m", "10" };
    if (trust)
    {
        common.AddRange(["--cacert", "server.pem"]);
    }

    if (login is not null)
    {
        common.AddRange(["-u", login]);
    }

    JsonElement sent = await Curl([.. common, "-d", "paid", url]);
    if (sent.GetProperty("exitCode").GetInt32() != 0)
    {
        string error = sent.GetProperty("standardError").GetProperty("content").GetString()!.Trim();
        Console.WriteLine($"curl exit {sent.GetProperty("exitCode")}" + (error.Contains("(67)") ? $": {error}" : ""));
        return;
    }

    // A publish is not answered: wait, with a bound, for the bus to report the message on the queue.
    await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "store_changed" }, ["store"] = "bus", ["name"] = "$queue/orders", ["timeout_ms"] = 10000,
    });
    JsonElement read = await Curl([.. common, url]);
    Console.WriteLine($"curl exit {read.GetProperty("exitCode")}: read {read.GetProperty("standardOutput").GetProperty("content").GetString()}");
}

// The curl tool runs curl's own arguments; the answer is JSON text with exitCode and the two streams.
async Task<JsonElement> Curl(string[] curlArguments)
{
    last = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    return JsonDocument.Parse(last.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}
