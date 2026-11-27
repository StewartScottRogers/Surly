// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an S3 bucket that takes uploads of at most 12 MiB through surl-mcp, and uploads a file in
// parts with curl-mcp's curl tool: start, one put per part, complete. server.pem and server.key are in
// the folder.
// Run: dotnet run program.cs -- <case>
//   upload   11,000,000 bytes in 5 MiB parts, then what the bucket holds (the default)
//   huge     13,000,000 bytes in 5 MiB parts, over the limit
//   small    11,000,000 bytes in 1,000,000-byte parts
#:package ModelContextProtocol.Core@2.4.0

using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "upload";

// The file to upload and the bucket folder, empty, both under the hosts' root.
string here = Directory.GetCurrentDirectory();
if (Directory.Exists("store")) Directory.Delete("store", recursive: true);
Directory.CreateDirectory(Path.Combine("store", "backups"));
Directory.CreateDirectory("work");
string key = which == "huge" ? "huge.tar" : "backup.tar";
byte[] data = System.Security.Cryptography.RandomNumberGenerator.GetBytes(which == "huge" ? 13_000_000 : 11_000_000);
File.WriteAllBytes(key, data);

// Both hosts are confined to this folder: SURL_MCP_ROOT and CURL_MCP_ROOT name it.
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

// The bucket server: TLS, one account, uploads allowed up to 12 MiB.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = new[] { "--directory", "store", "--cert", "server.pem", "--key", "server.key", "--user", "AKIAIOSFODNN7EXAMPLE:s3cret", "--allow-uploads", "--max-filesize", "12M" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int port = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;
string url = $"https://backups.s3.us-east-1.amazonaws.com/{key}";

// Start the upload; the answer, kept in a file, carries the UploadId.
(string status, _) = await Curl("-X", "POST", "-o", "work/answer", "-w", "%{http_code}", url + "?uploads");
Console.WriteLine($"initiate: {status}");
string upload = Regex.Match(File.ReadAllText("work/answer"), "<UploadId>([^<]+)</UploadId>").Groups[1].Value;

// One put per part, the part's bytes in a file of its own; each answer's ETag names the part.
int partSize = which == "small" ? 1_000_000 : 5 * 1024 * 1024;
string complete = "<CompleteMultipartUpload>";
bool refused = false;
for (int n = 1, offset = 0; offset < data.Length && !refused; n++, offset += partSize)
{
    File.WriteAllBytes($"work/part{n}", data[offset..Math.Min(offset + partSize, data.Length)]);
    (status, string etag) = await Curl("-T", $"work/part{n}", "-o", "work/answer", "-w", "%{http_code} %header{etag}", $"{url}?partNumber={n}&uploadId={upload}");
    Console.WriteLine($"part {n}: {status}");
    refused = status != "200";
    complete += $"<Part><PartNumber>{n}</PartNumber><ETag>{etag}</ETag></Part>";
}

// Complete with every part's number and ETag, or abort after a refusal.
if (!refused)
{
    File.WriteAllText("work/complete.xml", complete + "</CompleteMultipartUpload>");
    (status, _) = await Curl("--data-binary", "@work/complete.xml", "-o", "work/answer", "-w", "%{http_code}", $"{url}?uploadId={upload}");
    Console.WriteLine($"complete: {status}");
    refused = status != "200";
}

if (refused)
{
    Console.WriteLine($"refused: {Regex.Match(File.ReadAllText("work/answer"), "<Code>([^<]+)</Code>").Groups[1].Value}");
    (status, _) = await Curl("-X", "DELETE", "-o", "work/answer", "-w", "%{http_code}", $"{url}?uploadId={upload}");
    Console.WriteLine($"abort: {status}");
}

// surl-mcp list_files: what the bucket holds, and each object's size.
CallToolResult listed = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/backups" });
var entries = listed.StructuredContent!.Value.GetProperty("entries").EnumerateArray()
    .Select(e => $"{e.GetProperty("name").GetString()} {e.GetProperty("byte_count").GetInt64()} bytes").ToList();
Console.WriteLine($"bucket holds: {(entries.Count == 0 ? "nothing" : string.Join(", ", entries))}");
if (!refused)
    Console.WriteLine($"same as the file: {File.ReadAllBytes(Path.Combine("store", "backups", key)).AsSpan().SequenceEqual(data)}");
return 0;

// One curl tool call, signed with SigV4 to the bucket's host name at the local port. Answers what -w printed,
// split at its first space.
async Task<(string Status, string Etag)> Curl(params string[] words)
{
    string[] common = ["-s", "--max-time", "60", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{port}", "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", "AKIAIOSFODNN7EXAMPLE:s3cret"];
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = common.Concat(words).ToArray() });
    JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    string printed = run.GetProperty("standardOutput").GetProperty("content").GetString() ?? "";
    int space = printed.IndexOf(' ');
    return space < 0 ? (printed, "") : (printed[..space], printed[(space + 1)..]);
}
