// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs an S3 server that cuts every connection after 1,500,000 bytes through surl-mcp, fetches
// an object in byte ranges with curl-mcp's curl tool, and after a cut asks only for the bytes it is
// missing. server.pem and server.key are in the folder.
// Run: dotnet run program.cs -- <case>
//   range    bytes 0-1999999, cut and resumed (the default)
//   whole    both halves of the object, joined and compared with it
//   short    a range of 1,000,000 bytes, under the cut
//   refused  a range signed with a wrong secret
#:package ModelContextProtocol.Core@*

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "range";

// big.bin, in the bucket media of the folder the server will serve: 4,000,000 random bytes.
string here = Directory.GetCurrentDirectory();
Directory.CreateDirectory(Path.Combine("store", "media"));
Directory.CreateDirectory("dl");
byte[] big = System.Security.Cryptography.RandomNumberGenerator.GetBytes(4_000_000);
File.WriteAllBytes(Path.Combine("store", "media", "big.bin"), big);

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

// The bucket server: TLS, one account, and every connection closed cleanly once 1,500,000 bytes have gone out on it.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = new[] { "--directory", "store", "--cert", "server.pem", "--key", "server.key", "--user", "AKIAIOSFODNN7EXAMPLE:s3cret", "--fault", "drop;after-bytes=1500000;close=graceful" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int port = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;
string secret = which == "refused" ? "wrong" : "s3cret";
int cuts = 0;

switch (which)
{
    case "whole":
        bool both = await GetRange(0, 1_999_999, "dl/part0") && await GetRange(2_000_000, 3_999_999, "dl/part1");
        byte[] joined = [.. File.ReadAllBytes("dl/part0"), .. File.ReadAllBytes("dl/part1")];
        Console.WriteLine($"object whole: {(both && joined.AsSpan().SequenceEqual(big) ? "yes" : "no")}");
        break;
    case "short":
        await GetRange(1_000_000, 1_999_999, "dl/mid");
        break;
    default:
        if (await GetRange(0, 1_999_999, "dl/part0"))
            Console.WriteLine($"matches the object: {(File.ReadAllBytes("dl/part0").AsSpan().SequenceEqual(big.AsSpan(0, 2_000_000)) ? "yes" : "no")}");
        // surl-mcp get_events: the server's own account, one fault_applied event for each cut connection.
        CallToolResult events = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
        {
            ["server_id"] = id, ["kinds"] = new[] { "fault_applied" },
        });
        var kinds = events.StructuredContent!.Value.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("fault_kind").GetString()).ToList();
        Console.WriteLine($"fault_applied events: {kinds.Count} for {cuts} cut connections, kinds: {string.Join(",", kinds.Distinct())}");
        break;
}

return 0;

// The downloader: bytes first..last into the file. Each try is one curl tool call with -r, written to a
// file of its own and appended; after a try that ends short, the next asks only for the bytes after those
// the file holds; at most 5 tries. An error status (-f makes it exit 22) is not a cut: it stops there.
async Task<bool> GetRange(long first, long last, string file)
{
    File.WriteAllBytes(file, []);
    long fetched = 0;
    for (int tries = 0; tries < 5; tries++)
    {
        long from = first + new FileInfo(file).Length;
        if (from > last) break;
        CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
        {
            ["args"] = new[]
            {
                "-s", "-f", "--max-time", "30", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{port}",
                "--aws-sigv4", "aws:amz:us-east-1:s3", "-u", $"AKIAIOSFODNN7EXAMPLE:{secret}",
                "-r", $"{from}-{last}", "-o", file + ".try", "-w", "%{http_code} %{size_download}",
                "https://media.s3.us-east-1.amazonaws.com/big.bin",
            },
        });
        JsonElement run = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text).RootElement;
        int exit = run.GetProperty("exitCode").GetInt32();
        string got = run.GetProperty("standardOutput").GetProperty("content").GetString()!;
        Console.WriteLine($"bytes={from}-{last}: {got}, exitCode {exit}");
        if (got.StartsWith('4') || got.StartsWith('5')) return false;
        if (exit != 0) cuts++;
        fetched += long.Parse(got.Split(' ')[1]);
        if (File.Exists(file + ".try"))
        {
            using (FileStream to = new(file, FileMode.Append)) to.Write(File.ReadAllBytes(file + ".try"));
            File.Delete(file + ".try");
        }
    }

    bool whole = first + new FileInfo(file).Length > last;
    Console.WriteLine(whole ? $"fetched {fetched} bytes for a range of {last - first + 1}" : "gave up");
    return whole;
}
