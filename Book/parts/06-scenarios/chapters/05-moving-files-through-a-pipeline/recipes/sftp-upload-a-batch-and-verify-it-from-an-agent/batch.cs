// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent uploads a batch of files over SFTP to a pinned host key with curl-mcp, then lists the server's side and
// compares every name and size. host_key and host_key.pub come from setup: in recipe.yml.
// Run: dotnet run batch.cs [-- all|glob-refused|wrong-pin|missing|short|repaired|uploads-off|no-listing]
#:package ModelContextProtocol.Core@2.2.0

using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string scenario = args.Length > 0 ? args[0] : "all";

// The batch, written under the folder both hosts are confined to. Sizes are what the comparison checks.
string here = Directory.GetCurrentDirectory();
Directory.CreateDirectory("batch");
Directory.CreateDirectory("short");
File.WriteAllText("batch/notes.txt", "release notes\n");
File.WriteAllText("batch/config.ini", "[app]\nname = demo\n");
File.WriteAllText("batch/data.csv", "id,name\n1,a\n2,b\n");
File.WriteAllBytes("batch/blob.bin", new byte[1500]);
File.WriteAllBytes("short/blob.bin", new byte[1000]);
var sizes = Directory.GetFiles("batch").ToDictionary(Path.GetFileName, f => new FileInfo(f).Length)!;

// Both hosts start at once: each takes seconds to come up on a loaded machine.
Task<McpClient> surlStart = McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "surl-mcp", Command = "surl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["SURL_MCP_ROOT"] = here },
}));
Task<McpClient> curlStart = McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "curl-mcp", Command = "curl-mcp",
    EnvironmentVariables = new Dictionary<string, string?> { ["CURL_MCP_ROOT"] = here },
}));
await using McpClient surl = await surlStart;
await using McpClient curl = await curlStart;


// surl-mcp: an SFTP server on the host key from setup, with one account. Uploads and listings are on unless
// the scenario turns one off.
var words = new List<string> { "--hostkey", "host_key", "--user", "alice:s3cret" };
if (scenario != "uploads-off") words.Add("--allow-uploads");
if (scenario != "no-listing") words.Add("--list-directories");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "sftp://127.0.0.1:0" },
    ["arguments"] = words.ToArray(),
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string root = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!;

// The pin: the SHA-256 of the host key's blob, in base64 without padding, as ssh-keygen -l writes it.
byte[] blob = Convert.FromBase64String(File.ReadAllText("host_key.pub").Split(' ')[1]);
string pin = Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');
string wrongPin = new('A', 43);

// Each scenario is one call of the curl tool, which is one curl process: its -T pairs upload, in order, and the folder
// URL at the end lists /inbox/ on the same connection. (curl-mcp refuses a glob in -T, so each file has its own pair.)
string[] three = ["notes.txt", "config.ini", "data.csv"];
switch (scenario)
{
    case "all": await Run(pin, Pairs("batch", [.. sizes.Keys]), list: true); break;
    case "glob-refused":
        // curl would expand the glob itself; curl-mcp refuses it before curl runs, as a failure of kind PathRefused.
        JsonElement refused = await Curl("-s", "-S", "--hostpubsha256", pin, "-u", "alice:s3cret", "-T", "batch/{notes.txt,config.ini}", $"{root}inbox/");
        Console.WriteLine($"curl-mcp: {refused.GetProperty("failure").GetProperty("kind").GetString()}");
        break;
    case "wrong-pin": await Run(wrongPin, Pairs("batch", ["notes.txt"]), list: false); break;
    case "missing": await Run(pin, Pairs("batch", three), list: true); break;
    case "short": await Run(pin, [.. Pairs("batch", three), .. Pairs("short", ["blob.bin"])], list: true); break;
    case "repaired":
        // The short file first, then the whole batch over it, in one process.
        await Run(pin, [.. Pairs("batch", three), .. Pairs("short", ["blob.bin"]), .. Pairs("batch", [.. sizes.Keys])], list: true);
        break;
    case "uploads-off": await Run(pin, Pairs("batch", ["notes.txt"]), list: false); break;
    case "no-listing": await Run(pin, Pairs("batch", ["notes.txt"]), list: true); break;
}
return 0;

// The -T pairs for some files of a folder: the local path, then where it goes.
string[] Pairs(string folder, string[] names) => names.SelectMany(name => new[] { "-T", $"{folder}/{name}", $"{root}inbox/{name}" }).ToArray();

// One call: the pinned host key, the login, --ftp-create-dirs for /inbox/, the uploads, and the listing when asked.
// Without list, only the exit code matters; with it, a zero exit code carries the long listing, which is compared
// with the local batch: every name and size.
async Task Run(string hostPin, string[] uploads, bool list)
{
    var arguments = new List<string> { "-s", "-S", "--hostpubsha256", hostPin, "-u", "alice:s3cret", "--ftp-create-dirs" };
    arguments.AddRange(uploads);
    if (list) arguments.Add($"{root}inbox/");
    JsonElement answer = await Curl([.. arguments]);
    int code = answer.GetProperty("exitCode").GetInt32();
    if (code != 0) { Console.WriteLine($"{(list ? "listing refused" : "upload")}: exit code {code}"); return; }
    if (!list) { Console.WriteLine("upload: ok"); return; }

    var remote = answer.GetProperty("standardOutput").GetProperty("content").GetString()!
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimEnd('\r').Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .ToDictionary(fields => fields[^1], fields => long.Parse(fields[4]));
    var problems = new List<string>();
    foreach ((string name, long size) in sizes.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        if (!remote.TryGetValue(name, out long found)) problems.Add($"missing: {name} ({size})");
        else if (found != size) problems.Add($"size: {name} local {size}, remote {found}");
    }
    if (problems.Count > 0) { problems.ForEach(Console.WriteLine); return; }
    Console.WriteLine($"all {sizes.Count} files match: {string.Join(", ", remote.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"))}");

    // The store agrees: surl-mcp's list_files reads what the server holds, with byte counts.
    CallToolResult stored = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = "/inbox" });
    var held = stored.StructuredContent!.Value.GetProperty("entries").EnumerateArray()
        .ToDictionary(e => e.GetProperty("name").GetString()!, e => e.GetProperty("byte_count").GetInt64());
    Console.WriteLine($"the store agrees: {held.Count == sizes.Count && sizes.All(p => held.GetValueOrDefault(p.Key) == p.Value)}");
}

async Task<JsonElement> Curl(params string[] curlArgs)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = curlArgs });
    return JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement;
}
