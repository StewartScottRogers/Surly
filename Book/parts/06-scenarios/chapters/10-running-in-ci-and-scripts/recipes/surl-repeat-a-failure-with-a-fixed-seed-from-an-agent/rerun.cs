// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent reruns a failed test run with the seed its log printed: surl-mcp starts an S3 endpoint
// with --random-seed, curl-mcp runs the app's multipart upload, and read_file shows what was stored.
// Run: dotnet run rerun.cs [-- <seed> [probe] | nonces | bad-seed]
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using System.Text.RegularExpressions;
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

if (args is ["nonces"])
{
    // Two HTTP servers with the same seed and one account: compare their Digest nonces.
    string first = await Nonce(), second = await Nonce();
    Console.WriteLine($"same seed, same nonce: {first == second}");
    return 0;
}

if (args is ["bad-seed"])
{
    CallToolResult refused = await Start("http://127.0.0.1:0", "--random-seed", "soon");
    JsonElement why = refused.StructuredContent!.Value;
    Console.WriteLine($"tool error {refused.IsError == true}: exit_code {why.GetProperty("exit_code")}, {why.GetProperty("lines")[0]}");
    return 2;
}

// The seed the failed run's log printed.
string seed = args[0];
Console.WriteLine($"seed={seed}");
CallToolResult started = await Start("s3://127.0.0.1:0", "--random-seed", seed, "--s3-plaintext",
    "--user", "AKIAIOSFODNN7EXAMPLE:s3cret", "--allow-uploads");
JsonElement server = started.StructuredContent!.Value;
string serverId = server.GetProperty("server_id").GetString()!;
string endpoint = server.GetProperty("bound_urls")[0].GetString()!.Replace("s3://", "http://");

// A readiness check that connects is one more exchange before the app's.
if (args is [_, "probe"])
{
    await Curl("-s", "-o", "probe.out", endpoint);
}

// The app under test: a multipart upload of big.bin into the photos bucket, each step signed.
string[] sign = ["--aws-sigv4", "aws:amz:us-east-1:s3", "-u", "AKIAIOSFODNN7EXAMPLE:s3cret"];
string url = endpoint + "photos/big.bin";
string id = Regex.Match((await Curl([.. sign, "-sS", "-X", "POST", url + "?uploads"])).Output,
    "<UploadId>([0-9a-f]+)</UploadId>").Groups[1].Value;
Console.WriteLine($"upload {id}");

int result = 0;

// The bug the test run trips over, standing in for your app's: it uses the upload ID as a
// name, and a name may not begin with a digit. Only some IDs bring it out.
if (char.IsAsciiDigit(id[0]))
{
    Console.WriteLine($"failed: upload {id} cannot be named");
    result = 1;
}
else
{
    File.WriteAllText("part1.bin", "part one\n");
    string etag = Regex.Match((await Curl([.. sign, "-sS", "-D", "-", "-o", "part1.out", "-T", "part1.bin",
        $"{url}?partNumber=1&uploadId={id}"])).Output, "(?im)^etag: *(\\S+)").Groups[1].Value;
    File.WriteAllText("complete.xml",
        $"<CompleteMultipartUpload><Part><PartNumber>1</PartNumber><ETag>{etag}</ETag></Part></CompleteMultipartUpload>");
    (int exitCode, _) = await Curl([.. sign, "-sS", "-f", "-o", "complete.out", "--data-binary", "@complete.xml", $"{url}?uploadId={id}"]);
    Console.WriteLine(exitCode == 0 ? "stored big.bin" : $"complete: curl exit {exitCode}");
    result = exitCode;
}

// What the server recorded: the object, if the upload completed.
CallToolResult stored = await surl.CallToolAsync("read_file", new Dictionary<string, object?>
{
    ["server_id"] = serverId, ["path"] = "/photos/big.bin",
});
Console.WriteLine(stored.IsError == true
    ? "server holds no big.bin"
    : $"server holds big.bin: {stored.StructuredContent!.Value.GetProperty("text").GetString()!.TrimEnd()}");
return result;

// start_server with these listen URL and arguments; the bucket is a folder in its in-memory store.
async Task<CallToolResult> Start(string listenUrl, params string[] arguments) =>
    await surl.CallToolAsync("start_server", new Dictionary<string, object?>
    {
        ["listen_urls"] = new[] { listenUrl },
        ["arguments"] = arguments,
        ["files"] = new Dictionary<string, string> { ["photos/readme.txt"] = "the bucket the app uploads to\n" },
    });

// The curl tool with these arguments: curl's exit code and standard output.
async Task<(int ExitCode, string Output)> Curl(params string[] arguments)
{
    CallToolResult answer = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = arguments });
    JsonElement root = JsonDocument.Parse(answer.Content.OfType<TextContentBlock>().Single().Text).RootElement;
    return (root.GetProperty("exitCode").GetInt32(), root.GetProperty("standardOutput").GetProperty("content").GetString() ?? "");
}

// The first Digest nonce a fresh server with seed 3 and an account sends.
async Task<string> Nonce()
{
    JsonElement http = (await Start("http://127.0.0.1:0", "--random-seed", "3", "--user", "alice:s3cret")).StructuredContent!.Value;
    string headers = (await Curl("-s", "-o", "challenge.out", "-D", "-", http.GetProperty("bound_urls")[0].GetString() + "a.txt")).Output;
    return Regex.Match(headers, "nonce=\"([0-9a-f]+)\"").Groups[1].Value;
}
