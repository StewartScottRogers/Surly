// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent moves HTTP logins from Basic to Digest: surl-mcp runs a server that accepts both during the
// move, curl-mcp logs in each way, and the server's login events name the clients still on Basic, over
// the official MCP SDK.
// Run: dotnet run logins.cs [-- challenge|wrong|any|cutover]
//   (no argument) the old client with Basic, the new one with Digest, and what the server recorded
//   challenge     what a request with no login is offered during the move
//   wrong         a Basic login with the wrong password during the move
//   any           a client that lets curl pick the method from the challenge
//   cutover       a server that takes Digest only, which is surl's default over plain HTTP
#:package ModelContextProtocol.Core@2.2.0

using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "move";

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

// surl-mcp: one account per client, in surl's own words. Basic over plain HTTP is refused unless
// --allow-plaintext-auth says otherwise, so the move is the loosened server and "cutover" is not.
var surlArguments = new List<string> { "--user", "billing:b1lling", "--user", "search:s3arch" };
if (which != "cutover")
{
    surlArguments.Add("--allow-plaintext-auth");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["report.txt"] = "quarterly report\n" },
});
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string url = server.GetProperty("bound_urls")[0].GetString()! + "report.txt";

switch (which)
{
    case "move":
        string warning = server.GetProperty("log_lines").EnumerateArray().Select(l => l.GetString()!).First(l => l.Contains("warning"));
        Console.WriteLine(warning);
        Console.WriteLine($"billing, Basic: {await Status("--basic", "-u", "billing:b1lling", url)}");
        Console.WriteLine($"search, Digest: {await Status("--digest", "-u", "search:s3arch", url)}");
        string events = await Judged();
        Console.WriteLine($"still sending Basic: {string.Join(", ", Accounts(events, "Basic"))}");
        Console.WriteLine($"moved to Digest: {string.Join(", ", Accounts(events, "Digest"))}");
        Console.WriteLine($"events hold a password: {(events.Contains("b1lling") || events.Contains("s3arch") ? "yes" : "no")}");
        break;
    case "challenge":
        Console.WriteLine($"no login offers: {await Offers(url)}");
        break;
    case "wrong":
        Console.WriteLine($"billing, Basic, wrong password: {await Status("--basic", "-u", "billing:wrong", url)}");
        await Judged();
        break;
    case "any":
        Console.WriteLine($"billing, --anyauth: {await Status("--anyauth", "-u", "billing:b1lling", url)}");
        await Judged();
        break;
    case "cutover":
        Console.WriteLine($"billing, Basic: {await Status("--basic", "-u", "billing:b1lling", url)}");
        Console.WriteLine($"billing, Basic, offers: {await Offers(url, "--basic", "-u", "billing:b1lling")}");
        Console.WriteLine($"no login offers: {await Offers(url)}");
        Console.WriteLine($"search, Digest: {await Status("--digest", "-u", "search:s3arch", url)}");
        Console.WriteLine($"billing, --anyauth: {await Status("--anyauth", "-u", "billing:b1lling", url)}");
        Console.WriteLine($"Basic logins judged: {Accounts(await Events(), "Basic").Count()}");
        Console.WriteLine($"requests that arrived with Basic: {await ArrivedWithBasic()}");
        break;
}

return 0;

// One curl-mcp call: what curl wrote to standard output. curl-mcp answers in text, as JSON.
async Task<string> Curl(params string[] curlArguments)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArguments });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
}

// The status code of one GET; the body goes to a file in the folder.
async Task<string> Status(params string[] login) =>
    await Curl(["-s", "-o", "reply.txt", "-w", "%{http_code}", .. login]);

// The schemes the WWW-Authenticate headers of one answer offer, in order, read from curl -i.
async Task<string> Offers(string target, params string[] login)
{
    string reply = await Curl(["-s", "-i", .. login, target]);
    var offered = reply.Split('\n').Where(l => l.StartsWith("WWW-Authenticate:", StringComparison.OrdinalIgnoreCase))
        .Select(l => l.Split(' ')[1]).ToList();
    string status = reply.Split('\n')[0].Split(' ')[1];
    return $"{status} {(offered.Count == 0 ? "nothing" : string.Join(", ", offered))}";
}

// surl-mcp's login_judged events, as the JSON text get_events answers.
async Task<string> Events()
{
    CallToolResult result = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = serverId,
        ["kinds"] = new[] { "login_judged" },
    });
    return result.StructuredContent!.Value.GetRawText();
}

// Every login the server judged: method, account, verdict; and the events' text.
async Task<string> Judged()
{
    string text = await Events();
    foreach (JsonElement e in JsonDocument.Parse(text).RootElement.GetProperty("events").EnumerateArray())
    {
        Console.WriteLine($"login judged: {e.GetProperty("mechanism").GetString()} {e.GetProperty("account").GetString()} {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
    }

    return text;
}

// The accounts whose logins with one method were accepted: the clients that use it.
IEnumerable<string> Accounts(string text, string mechanism) =>
    JsonDocument.Parse(text).RootElement.GetProperty("events").EnumerateArray()
        .Where(e => e.GetProperty("mechanism").GetString() == mechanism && e.GetProperty("is_accepted").GetBoolean())
        .Select(e => e.GetProperty("account").GetString()!).Distinct();

// The exchanges whose received bytes carry a Basic Authorization header.
async Task<int> ArrivedWithBasic()
{
    CallToolResult result = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = serverId });
    return result.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().Count(x => x.GetProperty("chunks").EnumerateArray()
        .Where(c => c.GetProperty("direction").GetString() == "received")
        .Select(c => c.GetProperty("encoding").GetString() == "base64"
            ? Encoding.ASCII.GetString(Convert.FromBase64String(c.GetProperty("data").GetString()!))
            : c.GetProperty("data").GetString()!)
        .Any(d => d.Contains("Authorization: Basic ")));
}
