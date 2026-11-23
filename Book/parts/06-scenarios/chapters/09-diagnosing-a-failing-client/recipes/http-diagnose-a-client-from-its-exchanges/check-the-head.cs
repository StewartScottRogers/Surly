// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Four users' clients fetch a report through curl-mcp's curl tool, each with its own header settings; three get 400.
// For each one the agent waits for the exchange to end, reads the request head surl-mcp recorded and checks every
// header line against HTTP's rules for a field, then says which header is wrong. Run: dotnet run check-the-head.cs
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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

// The stand-in: HTTP on a free port, serving the report from memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["files"] = new Dictionary<string, string> { ["report.csv"] = "id,total\n1,42\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;
long lastSequence = 0;

// Each user's client, with the headers its settings add. One curl call is one connection, so one exchange.
var users = new (string Name, string[] Headers)[]
{
    ("ann", ["-H", "X-Request ID: 7"]),
    ("bob", ["-H", "Content-Length: ten"]),
    ("cy", ["-H", "Content-Length: 5", "-H", "Content-Length: 6"]),
    ("dee", ["-H", "X-Request-Id: 7"]),
};
foreach (var (name, headers) in users)
{
    string[] arguments = ["-s", "-m", "10", .. headers, url + "report.csv"];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    JsonElement answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string body = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    Console.WriteLine($"{name}'s client: curl exit {answer.GetProperty("exitCode")}, body {Quoted(body)}");

    // The diagnosis: the exchange as surl recorded it, once it has ended (waited for at most 10 s).
    CallToolResult ended = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["kinds"] = new[] { "exchange_ended" }, ["after_sequence"] = lastSequence, ["timeout_ms"] = 10000,
    });
    JsonElement end = ended.StructuredContent!.Value.GetProperty("event");
    lastSequence = end.GetProperty("sequence").GetInt64();
    CallToolResult recorded = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?>
    {
        ["server_id"] = server, ["after_exchange_id"] = end.GetProperty("exchange_id").GetInt64() - 1, ["max_exchanges"] = 1,
    });
    JsonElement chunks = recorded.StructuredContent!.Value.GetProperty("exchanges")[0].GetProperty("chunks");
    string received = string.Concat(chunks.EnumerateArray().Where(c => c.GetProperty("direction").GetString() == "received").Select(c => c.GetProperty("data").GetString()));
    string sent = string.Concat(chunks.EnumerateArray().Where(c => c.GetProperty("direction").GetString() == "sent").Select(c => c.GetProperty("data").GetString()));
    string status = sent.Split("\r\n")[0];
    string[] head = received.Split("\r\n\r\n")[0].Split("\r\n");
    List<string> wrong = Check(head[1..]);
    Console.WriteLine($"  server answered: {status}; {(wrong.Count == 0 ? "every header is well formed" : "wrong: " + string.Join("; ", wrong))}");
}

return 0;

// HTTP's rules for a header line (RFC 9110 section 5): the name is a token, the colon follows it at once, and a
// Content-Length is digits, given once or always the same.
static List<string> Check(string[] lines)
{
    var wrong = new List<string>();
    var lengths = new List<string>();
    foreach (string line in lines)
    {
        int colon = line.IndexOf(':');
        string name = colon < 0 ? line : line[..colon];
        string value = colon < 0 ? "" : line[(colon + 1)..].Trim(' ', '\t');
        if (colon < 0) wrong.Add($"\"{line}\" has no colon");
        else if (name.Length > 0 && name[^1] is ' ' or '\t') wrong.Add($"\"{line}\" has whitespace before its colon");
        else if (name.Length == 0 || !name.All(c => char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c)))
        {
            wrong.Add($"\"{line}\" has a name that is not a token ({string.Join(", ", name.Where(c => !char.IsAsciiLetterOrDigit(c) && !"!#$%&'*+-.^_`|~".Contains(c)).Select(c => $"'{c}'"))})");
        }
        else if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Length == 0 || !value.All(char.IsAsciiDigit)) wrong.Add($"\"{line}\" is not a number of bytes");
            lengths.Add(value);
        }
    }

    if (lengths.Distinct().Count() > 1) wrong.Add($"Content-Length is given {lengths.Count} times with different values ({string.Join(", ", lengths)})");
    return wrong;
}

static string Quoted(string text) => "\"" + text.Replace("\n", "\\n") + "\"";
