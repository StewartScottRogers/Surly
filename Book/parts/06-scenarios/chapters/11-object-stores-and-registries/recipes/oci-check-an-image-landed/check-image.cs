// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent checks that a pushed image landed: curl-mcp pushes it to a registry that surl-mcp serves, and the agent
// lists the registry's store with surl-mcp and checks that the tag, the manifest and every blob the manifest names
// are there, the right size, and hash to their names. server.pem, server.key, empty.json, layer1.txt, layer2.txt,
// manifest.json and broken.json are in the folder.
// Run: dotnet run check-image.cs [-- <case>]
//   landed (default)  push the image, then check it
//   layout            list the repository's folder
//   tag               the tag in index.json and the manifest's own hash
//   pull              pull the manifest by tag and compare it with the one pushed
//   no-manifest       push the blobs and not the manifest
//   missing-layer     push a manifest that names a layer that was never pushed
//   lost-layer        push the image, then delete a layer from the store
//   corrupt-layer     push the image, then change a layer's bytes in the store, keeping its length
//   digest-checked    push a file under a digest that is not its own
//   uploads-off       push to a registry started without --allow-uploads
//   wrong-password    push with the wrong password
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "landed";

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

// surl-mcp: an empty registry with one account.
var surlArguments = new List<string> { "--cert", "server.pem", "--key", "server.key", "--user", "alice:s3cret" };
if (which != "uploads-off") surlArguments.Add("--allow-uploads");
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "oci://127.0.0.1:0" },
    ["arguments"] = surlArguments,
});
string serverId = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
int port = new Uri(started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()!).Port;
string repo = $"oci://127.0.0.1:{port}/demo/app";

// What is pushed: the config, two layers and the manifest, each as a file named by what it is.
string Digest(string file) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
string config = Digest("empty.json"), layer1 = Digest("layer1.txt"), layer2 = Digest("layer2.txt"), manifest = Digest("manifest.json");
var blobs = new[] { ("empty.json", config), ("layer1.txt", layer1), ("layer2.txt", layer2) };

switch (which)
{
    case "layout":
        await Push(blobs, "manifest.json");
        Console.WriteLine($"demo/app: {string.Join(" ", (await List("/demo/app")).Select(e => $"{e.Name}({e.Kind})"))}");
        Console.WriteLine($"blobs: {(await List("/demo/app/blobs/sha256")).Count}");
        break;

    case "tag":
        await Push(blobs, "manifest.json");
        JsonElement entry = (await Tag())!.Value;
        Console.WriteLine($"index.json: tag 1.0 -> {entry.GetProperty("digest").GetString()}, {entry.GetProperty("size")} bytes");
        Console.WriteLine($"manifest.json hashes to that digest: {entry.GetProperty("digest").GetString() == manifest}");
        break;

    case "pull":
        await Push(blobs, "manifest.json");
        var (_, _, pulled) = await Curl(["-sS", "--cacert", "server.pem", "-u", "alice:s3cret", $"{repo}:1.0"]);
        Console.WriteLine($"pulled manifest identical to the pushed one: {pulled == File.ReadAllText("manifest.json")}");
        break;

    case "no-manifest":
        await Push(blobs, null);
        await Audit();
        break;

    case "missing-layer":
        // broken.json names a layer, sha256:b8fe6f0d..., that nobody pushed.
        var refused = await Push(blobs.Take(2).ToArray(), "broken.json");
        Console.WriteLine($"push: {Reason(refused)}");
        await Audit();
        break;

    case "lost-layer":
        await Push(blobs, "manifest.json");
        await surl.CallToolAsync("delete_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = $"/demo/app/blobs/sha256/{layer2[7..]}" });
        await Audit();
        break;

    case "corrupt-layer":
        // The same length as layer1.txt, other bytes: a size check passes it, a hash check does not.
        await Push(blobs, "manifest.json");
        await surl.CallToolAsync("put_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = $"/demo/app/blobs/sha256/{layer1[7..]}", ["text"] = "layer one bytez\n" });
        await Audit();
        break;

    case "digest-checked":
        // layer1.txt pushed under the digest of layer2.txt: curl hashes the file first and refuses it.
        var mismatch = await Curl(["-sS", "--cacert", "server.pem", "-u", "alice:s3cret", "-T", "layer1.txt", $"{repo}@{layer2}"]);
        Console.WriteLine($"push: {Reason(mismatch)}");
        Console.WriteLine($"blobs stored: {(await List("/demo/app/blobs/sha256")).Count}");
        break;

    case "uploads-off":
        Console.WriteLine($"push: {Reason(await Curl(["-sS", "--cacert", "server.pem", "-u", "alice:s3cret", "-T", "empty.json", $"{repo}@{config}"]))}");
        Console.WriteLine($"blobs stored: {(await List("/demo/app/blobs/sha256")).Count}");
        break;

    case "wrong-password":
        Console.WriteLine($"push: {Reason(await Curl(["-sS", "--cacert", "server.pem", "-u", "alice:wrong", "-T", "empty.json", $"{repo}@{config}"]))}");
        Console.WriteLine($"blobs stored: {(await List("/demo/app/blobs/sha256")).Count}");
        break;

    default:
        await Push(blobs, "manifest.json");
        await Audit();
        break;
}

return 0;

// The push: the blobs by digest, then the manifest under the tag 1.0, in one curl-mcp call, as the image's
// registry client would do it.
async Task<(int Exit, bool IsError, string Text)> Push((string File, string Digest)[] toPush, string? manifestFile)
{
    var argv = new List<string> { "-sS", "--cacert", "server.pem", "-u", "alice:s3cret" };
    foreach ((string file, string digest) in toPush) argv.AddRange(["-T", file, $"{repo}@{digest}"]);
    if (manifestFile is not null) argv.AddRange(["-T", manifestFile, $"{repo}:1.0"]);
    return await Curl(argv);
}

// The check an agent makes after a push: the tag, the manifest it points at, and each blob the manifest names.
async Task Audit()
{
    JsonElement? entry = await Tag();
    if (entry is null)
    {
        Console.WriteLine("index.json: none, so no tag points at a manifest");
        Console.WriteLine($"blobs in the store: {(await List("/demo/app/blobs/sha256")).Count}");
        Console.WriteLine("image 1.0 landed: no");
        return;
    }

    string manifestDigest = entry.Value.GetProperty("digest").GetString()!;
    Console.WriteLine($"tag 1.0 -> {manifestDigest[..19]}");
    var stored = (await List("/demo/app/blobs/sha256")).ToDictionary(e => e.Name, e => e.Size);
    JsonElement document = JsonDocument.Parse((await Read($"/demo/app/blobs/sha256/{manifestDigest[7..]}"))!).RootElement;

    // The manifest names a config and the layers; each is checked for being there, its size and its hash.
    var wanted = new List<(string Role, JsonElement Descriptor)> { ("config", document.GetProperty("config")) };
    wanted.AddRange(document.GetProperty("layers").EnumerateArray().Select(l => ("layer", l)));
    int problems = 0;
    foreach ((string role, JsonElement descriptor) in wanted)
    {
        string digest = descriptor.GetProperty("digest").GetString()!;
        long size = descriptor.GetProperty("size").GetInt64();
        string label = $"{role} {digest[..19]}";
        if (!stored.TryGetValue(digest[7..], out long onDisk))
        {
            Console.WriteLine($"{label}: MISSING");
            problems++;
        }
        else if (onDisk != size)
        {
            Console.WriteLine($"{label}: {onDisk} bytes, the manifest says {size}");
            problems++;
        }
        else if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes((await Read($"/demo/app/blobs/sha256/{digest[7..]}"))!))) != digest[7..])
        {
            Console.WriteLine($"{label}: {size} bytes as the manifest says, but the content does not hash to its name");
            problems++;
        }
        else
        {
            Console.WriteLine($"{label}: present, {size} bytes, hash ok");
        }
    }

    Console.WriteLine(problems == 0 ? $"image 1.0 landed: yes, the manifest and {wanted.Count} blobs" : $"image 1.0 landed: no, {problems} problem{(problems == 1 ? "" : "s")}");
}

// The manifest descriptor that index.json records under the tag 1.0, or null when there is no index.json.
async Task<JsonElement?> Tag()
{
    string? index = await Read("/demo/app/index.json");
    if (index is null) return null;
    return JsonDocument.Parse(index).RootElement.GetProperty("manifests").EnumerateArray()
        .First(m => m.GetProperty("annotations").GetProperty("org.opencontainers.image.ref.name").GetString() == "1.0").Clone();
}

// A file of the registry's store through surl-mcp's read_file, or null when it is not there (a tool error).
async Task<string?> Read(string path)
{
    CallToolResult result = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = path });
    return result.IsError == true ? null : result.StructuredContent!.Value.GetProperty("text").GetString();
}

// A folder of the store through list_files: name, kind and size of each child; empty when there is no such folder.
async Task<List<(string Name, string Kind, long Size)>> List(string path)
{
    CallToolResult result = await surl.CallToolAsync("list_files", new Dictionary<string, object?> { ["server_id"] = serverId, ["path"] = path });
    if (result.IsError == true) return [];
    return result.StructuredContent!.Value.GetProperty("entries").EnumerateArray()
        .Select(e => (e.GetProperty("name").GetString()!, e.GetProperty("kind").GetString()!, e.TryGetProperty("byte_count", out JsonElement n) && n.ValueKind == JsonValueKind.Number ? n.GetInt64() : 0L))
        .ToList();
}

// One curl-mcp call: curl's exit code, whether the tool called it an error, and standard output on success or
// standard error otherwise.
async Task<(int Exit, bool IsError, string Text)> Curl(IEnumerable<string> argv)
{
    CallToolResult result = await curl.CallToolAsync("curl", new Dictionary<string, object?> { ["args"] = argv.ToList() });
    using JsonDocument answer = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
    string Stream(string name) => (answer.RootElement.GetProperty(name).GetProperty("content").GetString() ?? "").Replace("\r", "");
    int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
    return (exit, result.IsError == true, exit == 0 ? Stream("standardOutput") : Stream("standardError").Trim());
}

// A push's outcome in one line: its exit code, and the registry's error code, or curl's own words, from its message.
string Reason((int Exit, bool IsError, string Text) call)
{
    if (call.Exit == 0) return "exit 0";
    Match code = Regex.Match(call.Text, @"\(([A-Z_]+)[ :)]");
    string detail = code.Success ? code.Groups[1].Value : call.Text[(call.Text.LastIndexOf(") ") + 2)..];
    return $"exit {call.Exit}, tool error: {(call.IsError ? "yes" : "no")}, {detail}";
}
