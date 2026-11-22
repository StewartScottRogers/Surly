// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent shows that surl-mcp's XMPP server requires TLS before a login, using the curl tool for every client, over the
// official MCP SDK. Run: dotnet run program.cs -- <case>
//   solution       the curl tool upgrades with STARTTLS, logs in and asks the server's disco info (the default)
//   offered        a raw client sends its stream header: what does the server offer?
//   early          a raw client sends a PLAIN login before STARTTLS
//   xmpps          the curl tool over xmpps://
//   untrusted      the curl tool without the certificate
//   wrong          a wrong password
//   no-login       no login
//   no-cert        a server with no certificate and no loosening
//   plain-offered  a server with --allow-plaintext-auth and no certificate: what does it offer?
//   plain-login    the same server accepts a PLAIN login in clear
//   plain-curl     the same server, the curl tool with no TLS
//   plain-wrong    the same server, a wrong password
//   recorded       what the server recorded of a STARTTLS login
//   recorded-early what the server recorded of a login before STARTTLS
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";
bool plain = which.StartsWith("plain-");

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

// The server: an account and, unless it is the plaintext one, a certificate and an xmpps listen URL beside xmpp.
var arguments = new List<string> { "--user", "alice:s3cret" };
var listen = new List<string> { "xmpp://127.0.0.1:0" };
if (plain)
{
    arguments.Add("--allow-plaintext-auth");
}
else if (which != "no-cert")
{
    arguments.AddRange(["--cert", cert, "--key", Path.Combine(here, "server.key")]);
    listen.Add("xmpps://127.0.0.1:0");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = listen,
    ["arguments"] = arguments,
});
if (which == "no-cert")
{
    JsonElement detail = started.StructuredContent!.Value;
    Console.WriteLine($"tool error: {(started.IsError == true ? "yes" : "no")}, exit_code {detail.GetProperty("exit_code")} ({detail.GetProperty("exit_code_name")})");
    Console.WriteLine(started.Content.OfType<TextContentBlock>().Single().Text);
    return 0;
}

string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string xmpp = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
string xmpps = listen.Count > 1 ? started.StructuredContent!.Value.GetProperty("bound_urls")[1].GetString()! : "";

switch (which)
{
    case "offered":
    case "plain-offered":
        Console.WriteLine(Between((await Raw("open.xml")).Out, "<stream:features>", "</stream:features>"));
        break;
    case "early":
    case "plain-login":
        Console.WriteLine(Regex.Match((await Raw("login.xml")).Out, "<failure.*</failure>|<success[^>]*>").Value);
        break;
    case "xmpps":
        await Disco(xmpps, "alice:s3cret", "--cacert", cert);
        break;
    case "untrusted":
        await Disco(xmpp, "alice:s3cret");
        break;
    case "wrong":
        await Disco(xmpp, "alice:wrong", "--cacert", cert);
        break;
    case "no-login":
        await Disco(xmpp, null, "--cacert", cert);
        break;
    case "plain-curl":
        await Disco(xmpp, "alice:s3cret");
        break;
    case "plain-wrong":
        await Disco(xmpp, "alice:wrong");
        break;
    case "recorded":
        await Curl("--cacert", cert, "-u", "alice:s3cret", xmpp + "127.0.0.1");
        await Recorded();
        break;
    case "recorded-early":
        await Raw("login.xml");
        await Recorded();
        break;
    default:
        await Disco(xmpp, "alice:s3cret", "--cacert", cert);
        break;
}

return 0;

static string Between(string text, string from, string to)
{
    int start = text.IndexOf(from, StringComparison.Ordinal);
    return text[start..(text.IndexOf(to, StringComparison.Ordinal) + to.Length)];
}

// The curl tool runs curl's own arguments: -s keeps the progress meter out, -S puts an error message back, and -u is the login.
async Task<(int Exit, string Out, string Error)> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = new[] { "-s", "-S" }.Concat(curlArguments).ToArray() });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (answer.GetProperty("exitCode").GetInt32(),
        answer.GetProperty("standardOutput").GetProperty("content").GetString() ?? "",
        answer.GetProperty("standardError").GetProperty("content").GetString()?.TrimEnd() ?? "");
}

// curl upgrades with STARTTLS itself when the server asks, logs in, and asks the server's domain what it is.
// A refusal is an exit code and a message, not a tool error; a TLS error's wording is the TLS library's, so only its code is shown.
async Task Disco(string url, string? login, params string[] more)
{
    var all = new List<string>(more);
    if (login is not null)
    {
        all.AddRange(["-u", login]);
    }

    all.Add(url + "127.0.0.1");
    var (exit, output, error) = await Curl(all.ToArray());
    Console.Write(exit == 0 ? output : exit == 60 ? $"refused: exit {exit}\n" : $"refused: exit {exit}, {error}\n");
}

// A raw client, by hand: telnet:// does nothing to the bytes, and -T uploads a file as they are. A telnet connection
// stays open, so -m 3 ends the wait for more, with exit 28, and what the server had sent by then is the answer.
async Task<(int Exit, string Out, string Error)> Raw(string file) =>
    await Curl("-m", "3", "-T", Path.Combine(here, file), "telnet://" + new Uri(xmpp).Authority);

// What the server recorded of its first exchange, as get_exchanges answers it: every chunk of it in order, decrypted,
// so what came before the upgrade and what came after are both in the record. The exchange ends a moment after the
// client has gone, so the agent waits for that event, with a bound, before it reads.
async Task Recorded()
{
    await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "exchange_ended" }, ["timeout_ms"] = 10000,
    });
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server });
    foreach (JsonElement chunk in result.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks").EnumerateArray())
    {
        string data = chunk.GetProperty("data").GetString()!;
        bool sent = chunk.GetProperty("direction").GetString() == "sent";
        if (data.Contains("<required/>")) { Console.WriteLine("server offers STARTTLS, required"); }
        else if (!sent && data.Contains("<starttls")) { Console.WriteLine("client asks for STARTTLS"); }
        else if (data.Contains("<proceed")) { Console.WriteLine("server proceeds"); }
        else if (data.Contains("<mechanisms")) { Console.WriteLine("server offers mechanisms"); }
        else if (!sent && data.Contains("<auth")) { Console.WriteLine("client sends its login"); }
        else if (data.Contains("<encryption-required/>")) { Console.WriteLine("server refuses it: encryption-required"); }
        else if (data.Contains("<success")) { Console.WriteLine("server accepts it"); }
    }
}
