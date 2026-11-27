// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks what an app's upload stored: surl-mcp serves S3 as plain HTTP, curl-mcp is the app that uploads,
// and read_file gives the stored bytes back, over the official MCP SDK. Run: dotnet run program.cs -- <case>
//   stored       the app uploads report.csv; the agent reads it back and compares it with the file (the default)
//   replace      the app uploads a second file to the same key
//   wrong-secret the app signs with a wrong secret
//   uploads-off  the server keeps its default and takes no uploads
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "stored";

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it. The files to upload are in it.
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

// An S3 server with one account and one bucket, photos, served as plain HTTP, which needs no certificate.
var arguments = new List<string> { "--user", "AKIAIOSFODNN7EXAMPLE:s3cret", "--s3-plaintext" };
if (which != "uploads-off")
{
    arguments.Add("--allow-uploads");
}

CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = arguments,
    ["files"] = new Dictionary<string, string> { ["photos/hello.txt"] = "hello from surl\n" },
});
string server = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int port = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;
string target = $"http://127.0.0.1:{port}/photos/reports/report.csv";
long lastSequence = 0;

// The app under test uploads a file, signed with the account's key and secret (AWS Signature Version 4).
string secret = which == "wrong-secret" ? "wrong" : "s3cret";
await Upload("report.csv");
if (which == "replace")
{
    await Upload("report-v2.csv");
}

// The check: the server's own copy, read back, and compared with the file the app uploaded.
string uploaded = which == "replace" ? "report-v2.csv" : "report.csv";
CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
{
    ["server_id"] = server, ["path"] = "/photos/reports/report.csv",
});
if (read.IsError == true)
{
    Console.WriteLine("read_file: tool error, nothing is stored");
    return 1;
}

JsonElement stored = read.StructuredContent!.Value;
string text = stored.GetProperty("text").GetString()!;
Console.WriteLine($"read_file: {stored.GetProperty("byte_count")} bytes, {stored.GetProperty("content_type").GetString()}");
Console.Write(text);
Console.WriteLine($"same as {uploaded}: {text == File.ReadAllText(uploaded)}");
return 0;

// curl-mcp's curl tool runs curl's own arguments. Over http:// a refusal is an ordinary response, so curl exits 0
// and the S3 error code is in the XML body on standard output.
async Task Upload(string file)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-sS", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", $"AKIAIOSFODNN7EXAMPLE:{secret}", "-T", file, target },
    });
    JsonElement answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string body = answer.GetProperty("standardOutput").GetProperty("content").GetString()!;
    Match code = Regex.Match(body, @"<Code>(\w+)</Code>");
    Console.WriteLine($"upload {file}: exit {answer.GetProperty("exitCode")}{(code.Success ? $", {code.Groups[1]}" : "")}");

    // The upload is stored when the server has recorded the write: wait for a change of this key that is
    // "written", at most ten seconds each time (two when nothing is expected). An event of an earlier upload,
    // such as its metadata, can come first, so each event seen moves the sequence number on.
    int timeout = which is "wrong-secret" or "uploads-off" ? 2000 : 10000;
    while (true)
    {
        CallToolResult waited = await surl.CallToolAsync("wait_for_event", new Dictionary<string, object?>
        {
            ["server_id"] = server, ["after_sequence"] = lastSequence, ["kinds"] = new[] { "store_changed" }, ["store"] = "content",
            ["name"] = "/photos/reports/report.csv", ["timeout_ms"] = timeout,
        });
        JsonElement found = waited.StructuredContent!.Value;
        if (!found.TryGetProperty("event", out JsonElement change))
        {
            Console.WriteLine("wait_for_event: nothing written within 2 s");
            return;
        }

        lastSequence = change.GetProperty("sequence").GetInt64();
        if (change.GetProperty("change").GetString() == "written")
        {
            Console.WriteLine("wait_for_event: written");
            return;
        }
    }
}
