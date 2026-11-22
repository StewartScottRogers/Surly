// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent syncs a folder to an S3 bucket: surl-mcp is the bucket, curl-mcp lists it with sizes and ETags and
// puts each file whose hash is not the ETag of the object of its name, and after one file changes puts only that
// one, which the listing and surl-mcp's events then prove. server.pem and server.key are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   sync (default)      sync the folder, list the bucket, compare the store with the folder
//   one-put-per-file    count the objects the server stored for the sync
//   listing-with-sizes  the signed listing's size and ETag of each key
//   resync-one-file     change one file and sync again
//   one-more-put        count the objects stored after the second sync
//   listing-proves-it   the listing before and after the second sync
//   changed-file-reads-back   get the changed object
//   nothing-changed     sync twice with no change between
//   wrong-secret        a put signed with the wrong secret
//   uploads-off         a put to a bucket started without --allow-uploads
//   nothing-arrived     a get after that refused put
//   listing-off         a listing of a bucket started without --list-directories
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "sync";

// The folder to sync, inside the folder both hosts are confined to: three small reports.
Directory.CreateDirectory("site");
File.WriteAllText("site/a.csv", "region,total\nnorth,1200\n");
File.WriteAllText("site/b.csv", "region,total\nsouth,950\n");
File.WriteAllText("site/c.csv", "region,total\neast,700\n");

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

// surl-mcp: the bucket, in memory, with an object that was there before the sync.
var surlArguments = new List<string> { "--cert", "server.pem", "--key", "server.key", "--user", "AKIAIOSFODNN7EXAMPLE:s3cret" };
if (which is not ("uploads-off" or "nothing-arrived")) surlArguments.Add("--allow-uploads");
if (which != "listing-off") surlArguments.Add("--list-directories");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "s3://127.0.0.1:0" },
    ["arguments"] = surlArguments,
    ["files"] = new Dictionary<string, string> { ["reports/old.csv"] = "stale\n" },
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int port = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;
string secret = which == "wrong-secret" ? "wrong" : "s3cret";

switch (which)
{
    case "events-debug":
        await Sync();
        Console.WriteLine((await Events()).GetRawText());
        break;

    case "one-put-per-file":
        await Sync();
        Console.WriteLine($"puts so far: {await Puts()}");
        break;

    case "listing-with-sizes":
        await Sync();
        foreach ((string key, (long size, string etag)) in await Remote()) Console.WriteLine($"{key} {size} {etag}");
        break;

    case "resync-one-file" or "one-more-put":
        await Sync();
        File.WriteAllText("site/b.csv", "region,total\nsouth,19500\n");
        Console.WriteLine($"changed: {string.Join(" ", await Sync())}");
        Console.WriteLine(which == "one-more-put" ? $"puts so far: {await Puts()}" : $"listing: {await ListNames()}");
        break;

    case "listing-proves-it":
        await Sync();
        var before = await Remote();
        File.WriteAllText("site/b.csv", "region,total\nsouth,19500\n");
        await Sync();
        foreach ((string key, (long size, string etag)) in await Remote())
        {
            (long oldSize, string oldEtag) = before[key];
            Console.WriteLine(oldEtag == etag ? $"{key}: unchanged, {size} bytes" : $"{key}: {oldSize} -> {size} bytes, new ETag");
        }

        break;

    case "changed-file-reads-back":
        await Sync();
        File.WriteAllText("site/b.csv", "region,total\nsouth,19500\n");
        await Sync();
        Console.Write((await Curl(["s3://reports/b.csv"])).Text);
        break;

    case "nothing-changed":
        await Sync();
        Console.WriteLine($"second sync put: {(await Sync() is { Count: > 0 } again ? string.Join(" ", again) : "nothing")}");
        Console.WriteLine($"puts so far: {await Puts()}");
        break;

    case "wrong-secret" or "uploads-off":
        Console.WriteLine($"put a.csv: {Refusal(await Put("a.csv"))}");
        break;

    case "nothing-arrived":
        Console.WriteLine($"put a.csv: {Refusal(await Put("a.csv"))}");
        Console.WriteLine($"get a.csv: {Refusal(await Curl(["s3://reports/a.csv"]))}");
        break;

    case "listing-off":
        Console.WriteLine($"list: {Refusal(await Curl(["s3://reports/"]))}");
        break;

    default:
        Console.WriteLine($"synced: {string.Join(" ", await Sync())}");
        Console.WriteLine($"listing: {await ListNames()}");
        // The store, read with surl-mcp's read_file, with no S3 in between.
        foreach (string file in Directory.GetFiles("site").Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(file);
            CallToolResult read = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = $"/reports/{name}" });
            Console.WriteLine($"{name} in the store matches the folder: {read.StructuredContent!.Value.GetProperty("text").GetString() == File.ReadAllText(file)}");
        }

        break;
}

return 0;

// A sync: put each file of site/ whose MD5 is not the ETag of the object of that name. Returns what it put.
async Task<List<string>> Sync()
{
    Dictionary<string, (long Size, string ETag)> remote = await Remote();
    var put = new List<string>();
    foreach (string file in Directory.GetFiles("site").Order(StringComparer.Ordinal))
    {
        string name = Path.GetFileName(file);
        string md5 = Convert.ToHexStringLower(MD5.HashData(File.ReadAllBytes(file)));
        if (remote.TryGetValue(name, out var known) && known.ETag == md5) continue;
        await Put(name);
        put.Add(name);
    }

    return put;
}

// A put of one file of site/ through s3://, in one curl-mcp call.
Task<(int Exit, bool IsError, string Text)> Put(string name) => Curl(["-T", $"site/{name}", $"s3://reports/{name}"]);

// The bucket's names, from the s3:// listing: one a line.
async Task<string> ListNames() => string.Join(" ", (await Curl(["s3://reports/"])).Text.Split('\n', StringSplitOptions.RemoveEmptyEntries));

// The size and ETag of each key, from ListObjectsV2 signed with AWS Signature Version 4 over HTTPS to the
// bucket's host name. The signed request is HTTP/1.1.
async Task<Dictionary<string, (long Size, string ETag)>> Remote()
{
    var (_, _, xml) = await Curl(["--http1.1", "--aws-sigv4", "aws:amz:us-east-1:s3", "https://reports.s3.us-east-1.amazonaws.com/?list-type=2"]);
    return Regex.Matches(xml, "<Key>([^<]*)</Key>.*?<ETag>\"([^\"]*)\"</ETag><Size>(\\d+)</Size>")
        .ToDictionary(m => m.Groups[1].Value, m => (long.Parse(m.Groups[3].Value), m.Groups[2].Value));
}

// One curl-mcp call. Every request goes to the server with the certificate and the login; the exit code, whether
// the tool called it an error, and the text: standard output on success, standard error otherwise.
async Task<(int Exit, bool IsError, string Text)> Curl(IEnumerable<string> argv)
{
    List<string> all = ["-sS", "--cacert", "server.pem", "--connect-to", $"::127.0.0.1:{port}", "-u", $"AKIAIOSFODNN7EXAMPLE:{secret}", .. argv];
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = all });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string Stream(string name) => (answer.RootElement.GetProperty(name).GetProperty("content").GetString() ?? "").Replace("\r", "");
    int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
    return (exit, result.IsError == true, exit == 0 ? Stream("standardOutput") : Stream("standardError").Trim());
}

// A refusal as one line: curl's exit code, whether the tool called it an error, and S3's status and error code.
string Refusal((int Exit, bool IsError, string Text) call)
{
    Match named = Regex.Match(call.Text, @"S3 returned error (\d+): (\w+)");
    return $"exit {call.Exit}, tool error: {(call.IsError ? "yes" : "no")}, {named.Groups[1]} {named.Groups[2]}";
}

// The content store's change events surl-mcp kept.
async Task<JsonElement> Events()
{
    CallToolResult result = await surl.CallToolAsync("get_events", new Dictionary<string, object?>
    {
        ["server_id"] = serverId, ["kinds"] = new[] { "store_changed" }, ["store"] = "content",
    });
    return result.StructuredContent!.Value;
}

// How many objects the server has stored for requests. The object that was there before the sync was seeded,
// not put, so it has no exchange behind it.
async Task<int> Puts() => (await Events()).GetProperty("events").EnumerateArray().Count(e => e.TryGetProperty("exchange_id", out JsonElement id) && id.ValueKind == JsonValueKind.Number);
