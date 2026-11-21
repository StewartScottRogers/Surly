// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent serves S3 as plain HTTP with surl-mcp and reaches it over http:// with curl-mcp, signed, over the
// official MCP SDK. Run: dotnet run program.cs
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. up.txt is in it.
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

// surl-mcp: two servers on one object each, in memory, with --s3-plaintext and no certificate. writable also
// allows uploads. start_server answers with the lines surl wrote on start, the warning among them.
async Task<(string Id, int Port, string[] Lines)> Start(params string[] more)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
        ["arguments"] = new[] { "--user", "AKIAIOSFODNN7EXAMPLE:s3cret" }.Concat(more).ToArray(),
        ["files"] = new Dictionary<string, string> { ["photos/hello.txt"] = "hello from surl\n" },
    });
    JsonElement answer = started.StructuredContent!.Value;
    string[] lines = answer.GetProperty("log_lines").EnumerateArray().Select(line => line.GetString()!).ToArray();
    return (answer.GetProperty("server_id").GetString()!, new Uri(answer.GetProperty("bound_urls")[0].GetString()!).Port, lines);
}

(string mainId, int mainPort, string[] mainLines) = await Start("--s3-plaintext");
(string writableId, int writablePort, _) = await Start("--s3-plaintext", "--allow-uploads");
Console.WriteLine($"warning: {mainLines.First(line => line.Contains("--s3-plaintext"))}");

// Without --s3-plaintext the same listen URL, with no certificate, is refused, a tool error that names surl's exit code.
CallToolResult refused = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = new[] { "--user", "AKIAIOSFODNN7EXAMPLE:s3cret" },
});
string refusal = refused.StructuredContent?.TryGetProperty("exit_code", out JsonElement code) == true ? code.ToString() : "none";
Console.WriteLine($"no certificate: tool error {refused.IsError}, exit {refusal}, {Regex.Match(string.Join(' ', refused.Content.OfType<TextContentBlock>().Select(t => t.Text)), @"needs a certificate").Value}");

string[] sign = ["--aws-sigv4", "aws:amz:us-east-1:s3", "-u"];
string[] login(string key, string secret) => [.. sign, $"{key}:{secret}"];
string Url(int port, string path) => $"http://127.0.0.1:{port}/{path}";

await Fetch("signed", Url(mainPort, "photos/hello.txt"), login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
// An SDK that puts the bucket in the host name: --connect-to sends the connection to the server.
await Fetch("virtual host", "http://photos.s3.us-east-1.amazonaws.com/hello.txt", [.. login("AKIAIOSFODNN7EXAMPLE", "s3cret"), "--connect-to", $"::127.0.0.1:{mainPort}"]);
await Fetch("unsigned", Url(mainPort, "photos/hello.txt"));
await Fetch("wrong secret", Url(mainPort, "photos/hello.txt"), login("AKIAIOSFODNN7EXAMPLE", "wrong"));
await Fetch("unknown key", Url(mainPort, "photos/hello.txt"), login("AKIAOTHEREXAMPLE", "s3cret"));
await Fetch("missing key", Url(mainPort, "photos/missing.txt"), login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
await Fetch("uploads off", Url(mainPort, "photos/up.txt"), [.. login("AKIAIOSFODNN7EXAMPLE", "s3cret"), "-T", "up.txt"]);
await Fetch("uploads on", Url(writablePort, "photos/up.txt"), [.. login("AKIAIOSFODNN7EXAMPLE", "s3cret"), "-T", "up.txt"]);
// curl's own s3:// URL is TLS from the first byte, so it cannot talk to the plain-HTTP listener.
await Fetch("s3 url", "s3://photos/hello.txt", ["-u", "AKIAIOSFODNN7EXAMPLE:s3cret", "--connect-to", $"::127.0.0.1:{mainPort}"]);

// surl-mcp reads back what the servers hold: the accepted put is an object, the refused one left none.
await Read("writable", writableId, "/photos/up.txt");
await Read("main", mainId, "/photos/up.txt");

// surl-mcp keeps what the server judged. An unsigned request carries no login, so it is never judged.
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
{
    ["server_id"] = mainId, ["kinds"] = new[] { "login_judged" },
});
foreach (JsonElement judged in events.StructuredContent!.Value.GetProperty("events").EnumerateArray())
{
    Console.WriteLine($"login_judged: {judged.GetProperty("account").GetString()} {(judged.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused")}");
}

return 0;

// curl-mcp's curl tool runs curl's own arguments and answers in text, as JSON. Over http:// a refusal is a
// response, so curl exits 0 and the S3 error code is in the XML body on standard output.
async Task Fetch(string label, string url, params string[] more)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS" }.Concat(more).Append(url).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
    string output = answer.RootElement.GetProperty("standardOutput").GetProperty("content").GetString()!.Replace("\r", "");
    string error = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!;
    Match code = Regex.Match(output, @"<Code>(\w+)</Code>");
    Console.WriteLine(exit != 0
        ? $"{label}: exit {exit} {Regex.Match(error, @"^curl: \(\d+\)").Value}"
        : code.Success ? $"{label}: exit 0 {code.Groups[1]}" : $"{label}: exit 0 {output.TrimEnd()}");
}

// surl-mcp's read_file answers the text of a file in the served store, or an error when there is none.
async Task Read(string label, string serverId, string path)
{
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = path });
    Console.WriteLine(read.IsError == true
        ? $"stored {label} {path}: nothing"
        : $"stored {label} {path}: {read.StructuredContent!.Value.GetProperty("text").GetString()!.TrimEnd()}");
}
