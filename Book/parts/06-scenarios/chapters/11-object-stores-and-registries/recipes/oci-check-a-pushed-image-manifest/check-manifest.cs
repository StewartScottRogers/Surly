// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a build's image push against an in-process registry: the build pushes with Curl.Api,
// and the test reads the registry's store through server.Files, finds the tag in index.json, reads
// the manifest it names, and checks that the manifest and every blob it names are there, the size
// the manifest says, and hash to their names.
// Run: dotnet run check-manifest.cs [-- <case>]
//   (no argument)    the whole image is pushed, and the check passes
//   no-manifest      the build pushes the blobs and not the manifest
//   missing-layer    the manifest names a layer the build never pushed
//   lost-layer       a layer is deleted from the store after the push
//   corrupt-layer    a layer is overwritten with other bytes of the same length
//   digest-checked   the build pushes a layer under a digest that is not its own
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "";

// The registry: TLS with a certificate it makes, one account, pushes allowed, the store in memory.
var options = new SurlServerOptions
{
    ListenUrls = { "oci://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("build", "b1ld") },
    AllowUploads = true,
};
await using SurlServer server = await SurlServer.StartAsync(options);
string repo = $"{server.BoundUrl}demo/app";
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};
await using CurlClient client = CurlClient.Create();

// The image: the empty config, two layers and the manifest that names them by digest and size.
byte[] config = "{}"u8.ToArray();
byte[] layer1 = "layer one bytes\n"u8.ToArray();
byte[] layer2 = "layer two is a bit longer\n"u8.ToArray();
byte[] manifest = Manifest(mode == "missing-layer" ? "never pushed\n"u8.ToArray() : layer2);

// The build's push: each blob by its digest, then the manifest under the tag 1.0.
var pushes = new List<(string Target, byte[] Bytes)> { ($"@{Digest(config)}", config), ($"@{Digest(layer1)}", layer1) };
if (mode != "missing-layer")
{
    pushes.Add((mode == "digest-checked" ? $"@{Digest(layer1)}" : $"@{Digest(layer2)}", layer2));
}

if (mode != "no-manifest")
{
    pushes.Add((":1.0", manifest));
}

try
{
    foreach ((string target, byte[] bytes) in pushes)
    {
        CurlResponse pushed = await client.SendAsync(new CurlRequest(HttpMethod.Put, repo + target)
        {
            Tls = trust,
            Authentication = new() { UserName = "build", Password = "b1ld" },
            Body = CurlContent.FromBytes(bytes),
        }, CancellationToken.None);
        await pushed.Body.DisposeAsync();
    }
}
catch (CurlException failure)
{
    Console.WriteLine($"push failed: {failure.Code} ({(int)failure.Code}) {failure.Message}");
}

if (mode == "lost-layer")
{
    await server.Files.DeleteAsync($"/demo/app/blobs/sha256/{Digest(layer2)[7..]}");
}
else if (mode == "corrupt-layer")
{
    await server.Files.PutAsync($"/demo/app/blobs/sha256/{Digest(layer1)[7..]}", "LAYER ONE BYTES\n"u8.ToArray());
}

// The check, from the store: the tag, the manifest it names, and each blob the manifest names.
int problems = 0;
string tagged;
try
{
    using JsonDocument index = JsonDocument.Parse((await server.Files.ReadAsync("/demo/app/index.json")).Bytes);
    JsonElement entry = index.RootElement.GetProperty("manifests").EnumerateArray()
        .First(m => m.GetProperty("annotations").GetProperty("org.opencontainers.image.ref.name").GetString() == "1.0");
    tagged = entry.GetProperty("digest").GetString()!;
    Console.WriteLine($"index.json: 1.0 -> {tagged[..19]}, {entry.GetProperty("size")} bytes");
}
catch (SurlNotFoundException)
{
    Console.WriteLine("index.json: none, so no tag points at a manifest");
    Console.WriteLine($"blobs in the store: {await CountBlobs()}");
    Console.WriteLine("FAIL: image 1.0 did not land");
    return 1;
}

byte[] stored = (await server.Files.ReadAsync($"/demo/app/blobs/sha256/{tagged[7..]}")).Bytes.ToArray();
Console.WriteLine($"manifest {tagged[..19]}: {(Digest(stored) == tagged ? "hash ok" : "HASH DIFFERS")}, the pushed bytes: {stored.AsSpan().SequenceEqual(manifest)}");
using JsonDocument parsed = JsonDocument.Parse(stored);
foreach (JsonElement descriptor in new[] { parsed.RootElement.GetProperty("config") }.Concat(parsed.RootElement.GetProperty("layers").EnumerateArray()))
{
    string digest = descriptor.GetProperty("digest").GetString()!;
    int size = descriptor.GetProperty("size").GetInt32();
    string verdict;
    try
    {
        byte[] blob = (await server.Files.ReadAsync($"/demo/app/blobs/sha256/{digest[7..]}")).Bytes.ToArray();
        verdict = blob.Length != size ? $"{blob.Length} bytes, the manifest says {size}"
            : Digest(blob) != digest ? $"{size} bytes as the manifest says, but the content does not hash to its name"
            : $"present, {size} bytes, hash ok";
    }
    catch (SurlNotFoundException)
    {
        verdict = "MISSING";
    }

    problems += verdict.StartsWith("present") ? 0 : 1;
    Console.WriteLine($"blob {digest[..19]}: {verdict}");
}

Console.WriteLine(problems == 0 ? "PASS: image 1.0 landed whole" : $"FAIL: image 1.0 landed with {problems} problem(s)");
return problems == 0 ? 0 : 1;

async Task<int> CountBlobs()
{
    try
    {
        return (await server.Files.ListAsync("/demo/app/blobs/sha256")).Entries.Count;
    }
    catch (SurlNotFoundException)
    {
        return 0;
    }
}

byte[] Manifest(byte[] secondLayer) => Encoding.ASCII.GetBytes(
    "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\","
    + $"\"config\":{{\"mediaType\":\"application/vnd.oci.empty.v1+json\",\"digest\":\"{Digest(config)}\",\"size\":{config.Length}}},"
    + $"\"layers\":[{{\"mediaType\":\"text/plain\",\"digest\":\"{Digest(layer1)}\",\"size\":{layer1.Length}}},"
    + $"{{\"mediaType\":\"text/plain\",\"digest\":\"{Digest(secondLayer)}\",\"size\":{secondLayer.Length}}}]}}\n");

static string Digest(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
