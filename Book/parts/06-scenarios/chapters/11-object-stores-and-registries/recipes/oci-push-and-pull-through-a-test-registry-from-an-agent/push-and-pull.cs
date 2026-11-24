// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An agent runs a build's push and a deploy's pull against a test registry: surl-mcp is the
// registry, curl-mcp's curl tool pushes and pulls, and the agent compares three digests, the
// manifest file the build pushed, the tag the registry recorded, and the manifest the deploy
// pulled, then checks each blob the manifest names.
// Run: dotnet run push-and-pull.cs -- <case>
//   tag        build 41 pushed under 1.0 and pulled by its tag (the default)
//   moved      a rebuild pushes build 42 under 1.0 before the pull
//   pinned     the same, but the pull is by the manifest's digest
//   unknown    the pull asks for a tag never pushed
//   changed    build 41's layer is overwritten in the store before the pull by digest
#:package ModelContextProtocol.Core@*

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

string which = args.Length > 0 ? args[0] : "tag";
const string Build41 = "sha256:33984613b0129bf0f7350dad5e7e372ccbfd585857d4cc9da2f1e333ac5b7603";

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

// The test registry: TLS with the certificate from setup, an account for the build and one for
// the deploy, pushes allowed, the store in memory.
CallToolResult started = await surl.CallToolAsync("start_server", new Dictionary<string, object?>
{
    ["listen_urls"] = new[] { "oci://127.0.0.1:0" },
    ["arguments"] = new[] { "--cert", "server.pem", "--key", "server.key", "--user", "build:b1ld", "--user", "deploy:d3ploy", "--allow-uploads" },
});
string id = started.StructuredContent!.Value.GetProperty("server_id").GetString()!;
string repo = started.StructuredContent!.Value.GetProperty("bound_urls")[0].GetString()! + "demo/app";

// The build: one call pushes the config, the layer and the manifest under 1.0.
(int pushExit, _) = await Curl("build:b1ld",
    "-T", "empty.json", $"{repo}@sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
    "-T", "app.txt", $"{repo}@{Build41}", "-T", "manifest.json", $"{repo}:1.0");
string pushed = Digest(File.ReadAllBytes(Path.Combine(here, "manifest.json")));
Console.WriteLine($"push: exit {pushExit}, manifest.json is {pushed}");
if (which is "moved" or "pinned")
{
    (int rebuilt, _) = await Curl("build:b1ld",
        "-T", "app-42.txt", $"{repo}@sha256:cdb288a305e18bfa3b50b6762520d8b133efab26e67ba430d803221c6bc976fc", "-T", "manifest-42.json", $"{repo}:1.0");
    Console.WriteLine($"rebuild push: exit {rebuilt}");
}

if (which == "changed")
{
    // Other bytes of the same length over build 41's layer, as the store's owner, not a client.
    await surl.CallToolAsync("put_file", new Dictionary<string, object?>
    {
        ["server_id"] = id, ["path"] = $"/demo/app/blobs/sha256/{Build41[7..]}", ["text"] = "app build 42\n",
    });
    Console.WriteLine("layer overwritten with put_file");
}

// What the registry recorded: the tag in index.json, and the digest it points at.
CallToolResult index = await surl.CallToolAsync("read_file", new Dictionary<string, object?> { ["server_id"] = id, ["path"] = "/demo/app/index.json" });
using (JsonDocument layout = JsonDocument.Parse(index.StructuredContent!.Value.GetProperty("text").GetString()!))
{
    foreach (JsonElement entry in layout.RootElement.GetProperty("manifests").EnumerateArray())
    {
        string tag = entry.TryGetProperty("annotations", out JsonElement notes) ? notes.GetProperty("org.opencontainers.image.ref.name").GetString()! : "(no tag)";
        Console.WriteLine($"index.json: {tag} -> {entry.GetProperty("digest").GetString()}");
    }
}

// The deploy: pull the manifest, compare, then pull and check each blob it names.
string reference = which switch { "pinned" or "changed" => "@" + pushed, "unknown" => ":2.0", _ => ":1.0" };
(int pullExit, string manifest) = await Curl("deploy:d3ploy", repo + reference);
if (pullExit != 0)
{
    Console.WriteLine($"pull {reference}: exit {pullExit}, {manifest}");
    return 1;
}

string got = Digest(Encoding.UTF8.GetBytes(manifest));
Console.WriteLine($"pulled {reference}: {got}");
if (got != pushed)
{
    Console.WriteLine("verdict: not the image the build pushed");
    return 1;
}

using JsonDocument parsed = JsonDocument.Parse(manifest);
foreach (JsonElement descriptor in new[] { parsed.RootElement.GetProperty("config") }.Concat(parsed.RootElement.GetProperty("layers").EnumerateArray()))
{
    string digest = descriptor.GetProperty("digest").GetString()!;
    (int exit, string blob) = await Curl("deploy:d3ploy", $"{repo}@{digest}");
    if (exit != 0)
    {
        Console.WriteLine($"blob {digest[..19]}: exit {exit}, {blob}");
        return 1;
    }

    Console.WriteLine($"blob {digest[..19]}: {Encoding.UTF8.GetByteCount(blob)} bytes, {(Digest(Encoding.UTF8.GetBytes(blob)) == digest ? "hash ok" : "HASH DIFFERS")}");
}

Console.WriteLine($"verdict: {reference} is the image the build pushed");
return 0;

// One call of the curl tool with the registry's certificate and an account; answers curl's exit
// code and standard output, or the first line of standard error when curl failed.
async Task<(int Exit, string Text)> Curl(string account, params string[] rest)
{
    CallToolResult ran = await curl.CallToolAsync("curl", new Dictionary<string, object?>
    {
        ["args"] = new[] { "-s", "-S", "--cacert", "server.pem", "-u", account }.Concat(rest).ToArray(),
    });
    using JsonDocument answer = JsonDocument.Parse(ran.Content.OfType<TextContentBlock>().Single().Text);
    int exit = answer.RootElement.GetProperty("exitCode").GetInt32();
    string stream = exit == 0 ? "standardOutput" : "standardError";
    string text = answer.RootElement.GetProperty(stream).GetProperty("content").GetString()!;
    return (exit, exit == 0 ? text : text.Split('\n')[0].TrimEnd('\r'));
}

static string Digest(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
