// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent uploads a file with curl-mcp's curl tool and -v to a surl-mcp server that refuses
// uploads, then lays curl's side beside the server's: get_exchanges and the login_judged events.
// Run: dotnet run both-sides.cs -- <case>
//   refused          log in with Digest; the server was not started with --allow-uploads (the default)
//   wrong-password   the same upload with the wrong password
//   no-login         the same upload with no login
//   allowed          log in with Digest; the server was started with --allow-uploads
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "refused";

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

// surl-mcp: one account; uploads only when the case asks for them.
var arguments = new List<string> { "--user", "alice:s3cret" };
if (which == "allowed")
{
    arguments.Add("--allow-uploads");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "http://127.0.0.1:0" },
    ["arguments"] = arguments.ToArray(),
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string url = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// curl-mcp: the upload, with -v writing curl's side of the conversation to standard error.
var line = new List<string> { "-s", "-v", "-T", "notes.txt", "-w", "%{http_code}" };
if (which != "no-login")
{
    line.AddRange(["--digest", "-u", which == "wrong-password" ? "alice:wrong" : "alice:s3cret"]);
}

line.Add(url + "notes.txt");
CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = line.ToArray() });
JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
Console.WriteLine($"http_code: {answer.GetProperty("standardOutput").GetProperty("content").GetString()}");

// curl's side: request lines it sent (>), status lines and Allow it received (<). Lines may end in CR.
foreach (string verbose in answer.GetProperty("standardError").GetProperty("content").GetString()!.Split('\n').Select(l => l.TrimEnd('\r')))
{
    if (verbose.StartsWith("> PUT ") || verbose.StartsWith("< HTTP/") || verbose.StartsWith("< Allow: "))
    {
        Console.WriteLine($"curl {(verbose[0] == '>' ? "sent" : "got")}: {verbose}");
    }
}

// The server's side: get_exchanges holds the bytes as they crossed; print each head's first line
// and the headers that explain the outcome.
CallToolResult seen = await surl.CallToolAsync("get_exchanges", new Dictionary<string, object?> { ["server_id"] = id });
foreach (JsonElement chunk in seen.StructuredContent!.Value.GetProperty("exchanges").EnumerateArray().SelectMany(e => e.GetProperty("chunks").EnumerateArray()))
{
    bool received = chunk.GetProperty("direction").GetString() == "received";
    string[] lines = chunk.GetProperty("data").GetString()!.Split("\r\n");
    if (lines[0].StartsWith(received ? "PUT " : "HTTP/"))
    {
        IEnumerable<string> telling = lines.Where(l => l.StartsWith("Authorization: ") || l.StartsWith("Allow: "))
            .Select(l => l.StartsWith("Authorization: ") ? l.Split(',')[0] : l);
        Console.WriteLine($"server {(received ? "received" : "sent")}: {string.Join(" | ", telling.Prepend(lines[0]))}");
    }
}

// Why: get_events says whether a login was judged, for whom, and the verdict.
CallToolResult judged = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = id, ["kinds"] = new[] { "login_judged" },
});
JsonElement events = judged.StructuredContent!.Value.GetProperty("events");
Console.WriteLine($"login_judged events: {events.GetArrayLength()}");
foreach (JsonElement e in events.EnumerateArray())
{
    Console.WriteLine($"  {e.GetProperty("account")} {e.GetProperty("mechanism")}: {(e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}

// The exit code says whether the curl call was a tool error.
Console.WriteLine($"tool error: {(result.IsError == true ? "yes" : "no")}");
return result.IsError == true ? 1 : 0;
