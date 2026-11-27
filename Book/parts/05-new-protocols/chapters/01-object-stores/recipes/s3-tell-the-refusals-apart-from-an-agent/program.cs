// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent produces each S3 refusal on purpose with surl-mcp and curl-mcp, names each from curl's error,
// and reads from the server which logins it judged. Over the official MCP SDK. Run: dotnet run program.cs
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. server.pem, server.key
// (a certificate and key for 127.0.0.1 and every bucket host name) and note.txt are in it.
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

// surl-mcp: two servers on one object each, in memory. main has one account and nothing loosened; open
// adds --allow-anonymous and --allow-uploads, to show that an unsigned put is still refused.
async Task<(string Id, int Port)> Start(params string[] more)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
        ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "AKIAIOSFODNN7EXAMPLE:s3cret" }.Concat(more).ToArray(),
        ["files"] = new Dictionary<string, string> { ["photos/hello.txt"] = "hello from surl\n" },
    });
    JsonElement answer = started.StructuredContent!.Value;
    return (answer.GetProperty("server_id").GetString()!, new Uri(answer.GetProperty("bound_urls")[0].GetString()!).Port);
}

(string mainId, int mainPort) = await Start();
(string openId, int openPort) = await Start("--allow-anonymous", "--allow-uploads");

const string Key = "AKIAIOSFODNN7EXAMPLE";
await Fetch("unsigned", mainPort, "s3://photos/hello.txt");
await Fetch("unknown key", mainPort, "s3://photos/hello.txt", "-u", "AKIAOTHEREXAMPLE:s3cret");
await Fetch("wrong secret", mainPort, "s3://photos/hello.txt", "-u", $"{Key}:wrong");
await Fetch("signed", mainPort, "s3://photos/hello.txt", "-u", $"{Key}:s3cret");
await Fetch("signed put", mainPort, "s3://photos/note.txt", "-u", $"{Key}:s3cret", "-T", "note.txt");
await Fetch("signed listing", mainPort, "s3://photos/", "-u", $"{Key}:s3cret");
await Fetch("missing key", mainPort, "s3://photos/missing.txt", "-u", $"{Key}:s3cret");
await Fetch("missing bucket", mainPort, "s3://videos/hello.txt", "-u", $"{Key}:s3cret");
await Fetch("anonymous read", openPort, "s3://photos/hello.txt");
await Fetch("anonymous write", openPort, "s3://photos/note.txt", "-T", "note.txt");
await Fetch("anonymous unknown key", openPort, "s3://photos/hello.txt", "-u", "AKIAOTHEREXAMPLE:s3cret");
await Fetch("anonymous wrong secret", openPort, "s3://photos/hello.txt", "-u", $"{Key}:wrong");

// surl-mcp keeps what the server judged. An unsigned request carries no login, so it is never judged;
// the other two are, each against the access key it named.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = mainId, ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement judged in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"login_judged: {judged.GetProperty("account").GetString()} {(judged.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}

return 0;

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. --connect-to sends the
// connection to the server whatever bucket host the URL names; --cacert trusts its certificate. The program
// prints curl's exit code, then the body, or the status and the S3 error code and message.
async Task Fetch(string label, int port, string url, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{port}" }.Concat(more).Append(url).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "");
    string error = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!;
    Match named = Regex.Match(error, @"returned error (\d+): (\w+): ([^\r\n]*)");
    Console.WriteLine(exit == 0
        ? $"{label}: exit 0 {output.TrimEnd()}"
        : $"{label}: exit {exit} {named.Groups[1]} {named.Groups[2]} ({named.Groups[3]})");
}
