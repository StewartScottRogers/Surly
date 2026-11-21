// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests an S3 client's signing: put, list and delete with one key pair through curl-mcp, the
// same steps with a wrong secret, then what surl-mcp recorded. server.pem and server.key are in the folder.
// Run: dotnet run program.cs -- <case>
//   run         the wrong secret first, then the right one, then the store and the judged logins (the default)
//   read-only   the right secret against a server with uploads off
#:package ModelContextProtocol.Core@2.2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "run";

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

// surl-mcp: one key pair, listings on, uploads on unless read-only, last quarter's report in the bucket.
var arguments = new List<string> { "--cert", "server.pem", "--key", "server.key", "--user", "AKIAIOSFODNN7EXAMPLE:s3cret", "--list-directories" };
if (which != "read-only")
{
    arguments.Add("--allow-uploads");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["reports/2026-q2.csv"] = "region,total\nnorth,1100\n" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int port = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;

// The run: put this quarter's report and list, delete last quarter's, list again.
foreach (string secret in which == "read-only" ? new[] { "s3cret" } : new[] { "wrong", "s3cret" })
{
    Console.WriteLine($"-- secret {secret}");
    string login = $"AKIAIOSFODNN7EXAMPLE:{secret}";
    await Curl("put, list", ["-u", login, "-T", "q3.csv", "s3://reports/2026-q3.csv", "s3://reports/"]);
    await Curl("delete", ["--aws-sigv4", "aws:amz:us-east-1:s3", "-u", login, "-X", "DELETE", "-w", "%{http_code}",
        "https://reports.s3.us-east-1.amazonaws.com/2026-q2.csv"]);
    await Curl("list", ["-u", login, "s3://reports/"]);
}

// surl-mcp: what the server holds, and each login it judged, in order.
CallToolResult listed = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/reports" });
Console.WriteLine($"list_files: {string.Join(", ", listed.StructuredContent!.Value.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("name").GetString()))}");
CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?> { ["server_id"] = id, ["kinds"] = new[] { "login_judged" } });
var judged = events.StructuredContent!.Value.GetProperty("events").EnumerateArray()
    .Select(e => e.GetProperty("is_accepted").GetBoolean() ? "accepted" : "refused");
Console.WriteLine($"login_judged ({events.StructuredContent!.Value.GetProperty("events")[0].GetProperty("account")}): {string.Join(" ", judged)}");
return 0;

// One curl-mcp call. --connect-to sends every bucket host to the server; --cacert trusts its certificate.
// The answer is text holding JSON; the program prints the exit code, the output on one line, and an S3 code.
async Task Curl(string label, List<string> step)
{
    List<string> argv = ["-sS", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{port}", .. step];
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    JsonElement root = answer.RootElement;
    string output = root.GetProperty("standardOutput").GetProperty("content").GetString()!;
    string errors = root.GetProperty("standardError").GetProperty("content").GetString()!;
    Match code = Regex.Match(output + errors, "<Code>(\\w+)</Code>|S3 returned error \\d+: (\\w+)");
    string shown = Regex.Replace(output, "<\\?xml.*</Error>", "", RegexOptions.Singleline).Replace("\r", "").Trim().Replace("\n", " ");
    Console.WriteLine($"{label}: exitCode {root.GetProperty("exitCode")} [{shown}]{(code.Success ? " " + code.Groups[1].Value + code.Groups[2].Value : "")}");
}
