// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent logs in to Cloud Storage with an HMAC key and with an OAuth token, refuses the wrong ones, and finds that
// writes need --allow-uploads, with surl-mcp and curl-mcp over the official MCP SDK. Run: dotnet run program.cs
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. server.pem, server.key
// (a certificate and key for 127.0.0.1 and Google's storage host) and note.txt are in it.
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

// surl-mcp: two servers on one object each, in memory. Both hold an HMAC key pair and, under an empty name,
// an OAuth token; writable adds --allow-uploads.
async Task<(string Id, int Port)> Start(params string[] more)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "gs://127.0.0.1:0" },
        ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "GOOGTESTKEY:s3cret", "--user", ":ya29.t0ken" }.Concat(more).ToArray(),
        ["files"] = new Dictionary<string, string> { ["photos/hello.txt"] = "hello from surl\n" },
    });
    JsonElement answer = started.StructuredContent!.Value;
    return (answer.GetProperty("server_id").GetString()!, new Uri(answer.GetProperty("bound_urls")[0].GetString()!).Port);
}

(string mainId, int mainPort) = await Start();
(string writableId, int writablePort) = await Start("--allow-uploads");

string[] hmac(string secret) => ["-u", $"GOOGTESTKEY:{secret}"];
string[] token(string t) => ["--oauth2-bearer", t];

await Fetch("hmac", mainPort, "gs://photos/hello.txt", hmac("s3cret"));
await Fetch("bearer", mainPort, "gs://photos/hello.txt", token("ya29.t0ken"));
await Fetch("wrong secret", mainPort, "gs://photos/hello.txt", hmac("wrong"));
await Fetch("unknown key", mainPort, "gs://photos/hello.txt", ["-u", "GOOGOTHERKEY:s3cret"]);
await Fetch("unknown token", mainPort, "gs://photos/hello.txt", token("ya29.other"));
await Fetch("no login", mainPort, "gs://photos/hello.txt");
await Fetch("uploads off hmac", mainPort, "gs://photos/note.txt", [.. hmac("s3cret"), "-T", "note.txt"]);
await Fetch("uploads off bearer", mainPort, "gs://photos/note.txt", [.. token("ya29.t0ken"), "-T", "note.txt"]);
await Fetch("uploads on hmac", writablePort, "gs://photos/note.txt", [.. hmac("s3cret"), "-T", "note.txt"]);
await Fetch("uploads on bearer", writablePort, "gs://photos/token.txt", [.. token("ya29.t0ken"), "-T", "note.txt"]);

// surl-mcp reads back what the servers hold: the accepted puts are objects, the refused one left none.
await Read("writable", writableId, "/photos/note.txt");
await Read("writable", writableId, "/photos/token.txt");
await Read("main", mainId, "/photos/note.txt");

// surl-mcp keeps what the server judged. A request with no login names no account, so it is never judged;
// the others are, each against the name it carried (a token is judged as the account "bearer token").
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = mainId, ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement judged in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"login_judged: [{judged.GetProperty("account").GetString()}] {(judged.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}

return 0;

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. --connect-to sends the
// connection to the server whatever host the URL names; --cacert trusts its certificate. The program prints
// curl's exit code, then the body, or the status and the error code from standard error.
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
    Match named = Regex.Match(error, @"returned error (\d+): (\w+)");
    Console.WriteLine(exit == 0
        ? $"{label}: exit 0 {output.TrimEnd()}"
        : $"{label}: exit {exit} {named.Groups[1]} {named.Groups[2]}");
}

// surl-mcp's read_file answers the text of a file in the served store, or an error when there is none.
async Task Read(string label, string serverId, string path)
{
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = path });
    Console.WriteLine(read.IsError == true
        ? $"stored {label} {path}: nothing"
        : $"stored {label} {path}: {read.StructuredContent!.Value.GetProperty("text").GetString()!.TrimEnd()}");
}
