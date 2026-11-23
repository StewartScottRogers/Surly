// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks a ported client: the old curl command line and the new field-based request are
// both sent to one surl-mcp server, and the requests it recorded are compared. Run: dotnet run program.cs -- <case>
//   solution  the port forgets three headers; the agent finds them and sends the port again
//   body      the port also sends its body with a newline the command line did not
//   no-agent  the port leaves the user agent out
//   no-method the port leaves the method out
//   plain     a port of a plain command line, which matches from the start
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "solution";

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

// The server that records what both clients send.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = new[] { "--allow-anonymous", "--allow-uploads" },
    ["files"] = new Dictionary<string, string> { ["report.txt"] = "report ready\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The old client: a curl command line. The new client: the same request as fields.
string[] oldArgs = which == "plain"
    ? ["-s", "-A", "nightly/2", "-H", "Accept: application/json", "-H", "X-Api-Key: k1", "-o", "old.out", url + "report.txt"]
    : ["-s", "--compressed", "-u", "deploy:secret", "-e", "https://app.example/", "-A", "nightly/2",
       "-H", "Accept: application/json", "-H", "X-Api-Key: k1", "-d", "a=1", "-o", "old.out", url + "in.txt"];
await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = oldArgs });
string oldHead = await Head(1);

var headers = new List<(string Name, string Value)> { ("Accept", "application/json"), ("X-Api-Key", "k1") };
string port = which == "plain" ? "report.txt" : "in.txt";
string body = which == "body" ? "a=1\n" : "a=1";
await Send(headers);
string newHead = await Head(2);
Console.WriteLine($"port, first try: {Compare(oldHead, newHead)}");

if (which != "plain")
{
    // The agent copies what the record shows the old client sent and the port did not, then sends again.
    var wanted = Parse(oldHead).Where(h => !Parse(newHead).Any(n => Same(n.Name, h.Name))).ToList();
    headers.AddRange(wanted);
    await Send(headers);
    Console.WriteLine($"port, second try: {Compare(oldHead, await Head(3))}");
}

return 0;

// The port: the same request as fields, through curl-mcp's transfer. -d makes a POST, so the port says POST.
async Task Send(List<(string Name, string Value)> sent)
{
    var arguments = new Dictionary<string, object?>
    {
        ["url"] = url + port,
        ["headers"] = new JsonArray(sent.Select(h => (JsonNode)new JsonObject { ["name"] = h.Name, ["value"] = h.Value }).ToArray()),
    };
    if (which != "no-agent") arguments["userAgent"] = "nightly/2";
    if (which != "plain")
    {
        if (which != "no-method") arguments["method"] = "POST";
        arguments["body"] = new Dictionary<string, object?> { ["content"] = body, ["isBase64"] = false };
    }

    await curl.CallToolAsync("transfer", arguments);
}

// The head of the nth exchange the server recorded: its request line, headers and body as received.
// An exchange ends a moment after the client has its answer: wait for n of them, for at most 10 s.
async Task<string> Head(int n)
{
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
    {
        JsonElement ended = (await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["kinds"] = new[] { "exchange_ended" },
        })).StructuredContent!.Value.GetProperty("events");
        if (ended.GetArrayLength() >= n) break;
    }

    JsonElement exchanges = (await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = server })).StructuredContent!.Value.GetProperty("exchanges");
    return string.Concat(exchanges.EnumerateArray().First(e => e.GetProperty("exchange_id").GetInt64() == n).GetProperty("chunks").EnumerateArray()
        .Where(c => c.GetProperty("direction").GetString() == "received").Select(c => c.GetProperty("data").GetString()));
}

// A head's headers, in order, without the request line, the blank line and the body.
static List<(string Name, string Value)> Parse(string head) => head.Split("\r\n\r\n")[0].Split("\r\n").Skip(1)
    .Select(l => (l[..l.IndexOf(':')], l[(l.IndexOf(':') + 1)..].Trim())).ToList();

static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

// What differs between two recorded requests; Host is the same server and is left out.
static string Compare(string oldHead, string newHead)
{
    var differences = new List<string>();
    if (oldHead.Split("\r\n")[0] != newHead.Split("\r\n")[0]) differences.Add("request line");
    var a = Parse(oldHead).Where(h => !Same(h.Name, "Host")).ToList();
    var b = Parse(newHead).Where(h => !Same(h.Name, "Host")).ToList();
    differences.AddRange(a.Where(h => !b.Any(n => Same(n.Name, h.Name))).Select(h => $"only the old client sent {h.Name}"));
    differences.AddRange(b.Where(h => !a.Any(n => Same(n.Name, h.Name))).Select(h => $"only the port sent {h.Name}"));
    differences.AddRange(a.Where(h => b.Any(n => Same(n.Name, h.Name) && n.Value != h.Value)).Select(h => $"{h.Name} differs: old {h.Value}, port {b.First(n => Same(n.Name, h.Name)).Value}"));
    if (oldHead.Split("\r\n\r\n", 2)[1] != newHead.Split("\r\n\r\n", 2)[1]) differences.Add("body");
    return differences.Count == 0 ? "the two requests match" : string.Join("; ", differences);
}
