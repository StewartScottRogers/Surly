// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an origin with its own account and two proxies with a proxy account (surl-mcp),
// logs in to each hop with curl-mcp, and reads back which logins the server judged.
// Run: dotnet run proxy-login.cs -- <case>
//   fetch         Digest to the proxy and Digest to the origin, with the curl tool (the default)
//   no-login      transfer through the proxy with no login: the 407 and what it offers
//   wrong         a wrong proxy password
//   basic-http    transfer with the login in the proxy URL, which sends Basic
//   https-offers  no proxy login to the https proxy: what its 407 offers
//   basic-https   Basic to the https proxy
//   plaintext     Basic to an http proxy started with --allow-plaintext-auth
//   swapped       the origin's account at the proxy, then the proxy's account at the origin
//   events        the logins each listener judged during a fetch
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "fetch";

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

// One server: the origin with account web, an http and an https proxy sharing proxy account app.
List<string> arguments = ["--user", "web:site-pw", "--proxy", "http://127.0.0.1:0", "--proxy", "https://127.0.0.1:0",
    "--cert", "server.pem", "--key", "server.key", "-U", "app:proxy-pw"];
if (which == "plaintext") arguments.Add("--allow-plaintext-auth");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["hello.txt"] = "hello from surl\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string origin = server.GetProperty("bound_urls")[0].GetString()!;
string httpProxy = server.GetProperty("bound_urls")[1].GetString()!;
string httpsProxy = server.GetProperty("bound_urls")[2].GetString()!;
string file = origin + "hello.txt";

// The curl tool takes curl's own words and answers in text, as JSON: the exit code and standard output.
async Task<string> Curl(params string[] words)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = words });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return $"exitCode {run.GetProperty("exitCode")}: {run.GetProperty("standardOutput").GetProperty("content").GetString()!.TrimEnd()}";
}

// "Digest realm=..., algorithm=SHA-256, ..." becomes "Digest SHA-256"; "Basic realm=..." becomes "Basic".
static string Offer(string value)
{
    int at = value.IndexOf("algorithm=");
    return value.Split(' ')[0] + (at < 0 ? "" : " " + value[(at + 10)..].Split(',')[0]);
}

switch (which)
{
    case "fetch":
        Console.WriteLine(await Curl("-s", "-x", httpProxy, "--proxy-digest", "-U", "app:proxy-pw", "--digest", "-u", "web:site-pw", file));
        break;
    case "no-login":
    case "basic-http":
    {
        // transfer has a proxy field but no proxy login: a user and password in the proxy URL go as Basic.
        string proxy = which == "no-login" ? httpProxy : httpProxy.Replace("http://", "http://app:proxy-pw@");
        CallToolResult fetched = await curl.CallToolAsync("transfer", new Dictionary<string, object?> { ["url"] = file, ["proxy"] = proxy });
        JsonElement answer = JsonDocument.Parse(fetched.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        Console.WriteLine($"statusCode: {answer.GetProperty("statusCode")}, failure: {answer.GetProperty("failure").GetRawText()}, tool error: {(fetched.IsError == true ? "yes" : "no")}");
        foreach (JsonElement h in answer.GetProperty("headers").EnumerateArray().Where(h => h.GetProperty("name").GetString() == "Proxy-Authenticate"))
            Console.WriteLine($"offers: {Offer(h.GetProperty("value").GetString()!)}");
        break;
    }
    case "wrong":
        Console.WriteLine(await Curl("-s", "-o", "out.txt", "-w", "%{http_code}", "-x", httpProxy, "--proxy-digest", "-U", "app:guess", "--digest", "-u", "web:site-pw", file));
        break;
    case "https-offers":
    {
        // -D - puts the answer's headers in standard output; --proxy-cacert trusts the proxy's certificate.
        string head = await Curl("-s", "-o", "out.txt", "-D", "-", "--proxy-cacert", "server.pem", "-x", httpsProxy, file);
        Console.WriteLine(head.Split("\r\n")[0]);
        foreach (string line in head.Split("\r\n").Where(l => l.StartsWith("Proxy-Authenticate: ")))
            Console.WriteLine($"offers: {Offer(line["Proxy-Authenticate: ".Length..])}");
        break;
    }
    case "basic-https":
        Console.WriteLine(await Curl("-s", "--proxy-cacert", "server.pem", "-x", httpsProxy, "-U", "app:proxy-pw", "--digest", "-u", "web:site-pw", file));
        break;
    case "plaintext":
        Console.WriteLine(server.GetProperty("log_lines")[0].GetString());
        Console.WriteLine(await Curl("-s", "-x", httpProxy, "-U", "app:proxy-pw", "--digest", "-u", "web:site-pw", file));
        break;
    case "swapped":
        Console.WriteLine("origin account at the proxy: " + await Curl("-s", "-o", "out.txt", "-w", "%{http_code}", "-x", httpProxy, "--proxy-digest", "-U", "web:site-pw", "--digest", "-u", "web:site-pw", file));
        Console.WriteLine("proxy account at the origin: " + await Curl("-s", "-o", "out.txt", "-w", "%{http_code}", "-x", httpProxy, "--proxy-digest", "-U", "app:proxy-pw", "--digest", "-u", "app:proxy-pw", file));
        break;
    case "events":
        await Curl("-s", "-o", "out.txt", "-x", httpProxy, "--proxy-digest", "-U", "app:proxy-pw", "--digest", "-u", "web:site-pw", file);
        foreach (var (name, listener) in new[] { ("proxy", httpProxy), ("origin", origin) })
        {
            CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
            {
                ["server_id"] = serverId, ["kinds"] = new[] { "login_judged" }, ["listen_url"] = listener,
            });
            foreach (JsonElement e in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
                Console.WriteLine($"{name}: {e.GetProperty("mechanism").GetString()} {e.GetProperty("account").GetString()} {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
        }
        break;
}
return 0;
