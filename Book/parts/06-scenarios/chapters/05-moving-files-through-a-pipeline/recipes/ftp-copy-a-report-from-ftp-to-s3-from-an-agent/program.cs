// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent tests a nightly job: one curl-mcp call downloads a report over FTP with TLS and puts it in an
// S3 bucket; surl-mcp's read_file then compares the object with the source. server.pem and server.key
// are in the folder.
// Run: dotnet run program.cs -- <case>
//   copy           the hop for out/report.csv (the default)
//   missing        the hop for a report the FTP server does not have, with --fail-early
//   stale          the same, without --fail-early, after a good run
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "copy";

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

// surl-mcp: the FTP server with tonight's report, and the S3 server with a bucket. Both in memory.
const string Report = "region,total\r\nnorth,1200\r\nsouth,950\r\n";
(string Id, int Port) ftp = await Start("ftp://127.0.0.1:0", ["--user", "nightly:s3cret"], new() { ["out/report.csv"] = Report });
(string Id, int Port) s3 = await Start("s3://127.0.0.1:0", ["--user", "AKIAIOSFODNN7EXAMPLE:s3cret", "--allow-uploads"],
    new() { ["reports/2026-11-19.csv"] = "region,total\r\nnorth,1180\r\nsouth,940\r\n" });

// The job: one curl call, the download, --next, the upload.
if (which == "stale")
{
    await Hop("out/report.csv", "2026-11-20.csv", failEarly: true);
    await Hop("out/missing.csv", "2026-11-21.csv", failEarly: false);
}
else
{
    await Hop(which == "missing" ? "out/missing.csv" : "out/report.csv", which == "missing" ? "2026-11-21.csv" : "2026-11-20.csv", failEarly: true);
}

// surl-mcp: compare the object with the source, both read from the servers' stores.
string source = await Read(ftp.Id, "/out/report.csv") ?? "";
foreach (string date in new[] { "2026-11-20.csv", "2026-11-21.csv" })
{
    string? stored = await Read(s3.Id, "/reports/" + date);
    Console.WriteLine($"{date}: {(stored is null ? "none" : stored == source ? $"{stored.Length} bytes, identical to the source" : "different from the source")}");
}

return 0;

async Task<(string, int)> Start(string listen, string[] extra, Dictionary<string, string> files)
{
    CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { listen },
        ["arguments"] = (string[])["--cert", "server.pem", "--key", "server.key", .. extra],
        ["files"] = files,
    });
    JsonElement server = started.StructuredContent!.Value;
    return (server.GetProperty("server_id").GetString()!, new Uri(server.GetProperty("bound_urls")[0].GetString()!).Port);
}

async Task Hop(string from, string to, bool failEarly)
{
    List<string> argv = [.. failEarly ? new[] { "--fail-early" } : [], "-sS",
        "--ssl-reqd", "--cacert", "server.pem", "-u", "nightly:s3cret", "-o", "report.csv", $"ftp://127.0.0.1:{ftp.Port}/{from}",
        "--next", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{s3.Port}", "-u", "AKIAIOSFODNN7EXAMPLE:s3cret",
        "-T", "report.csv", $"s3://reports/{to}"];
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string errors = answer.RootElement.GetProperty("standardError").GetProperty("content").GetString()!.Replace("\r", "").Trim();
    Console.WriteLine($"{from} -> {to}: exitCode {answer.RootElement.GetProperty("exitCode")}, tool error {(result.IsError == true ? "yes" : "no")}{(errors.Length > 0 ? ", " + errors : "")}");
}

async Task<string?> Read(string id, string path)
{
    CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = path });
    return read.IsError == true ? null : read.StructuredContent!.Value.GetProperty("text").GetString();
}
