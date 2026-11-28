// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a build that pushes an image and a deploy that pulls it, against an in-process
// registry: the deploy checks that the manifest it pulled hashes to the digest the build pushed,
// then pulls each blob the manifest names and checks it the same way.
// Run: dotnet run build-and-deploy.cs [-- <case>]
//   (no argument)  build 41 is pushed under 1.0 and deployed by its tag
//   moved          a rebuild pushes build 42 under 1.0 before the deploy of 1.0
//   pinned         the same, but the deploy pulls by the manifest's digest
//   unknown        the deploy asks for a tag never pushed
//   password       the deploy logs in with the wrong password
//   changed        build 41's layer is overwritten in the store before the deploy by digest
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "";

// The test registry: TLS with a certificate it makes, an account for the build and one for the
// deploy, pushes allowed, the store in memory.
var options = new SurlServerOptions
{
    ListenUrls = { "oci://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("build", "b1ld"), new SurlAccount("deploy", "d3ploy") },
    AllowUploads = true,
};
await using SurlServer server = await SurlServer.StartAsync(options);
string repo = $"{server.BoundUrl}demo/app";

// The client trusts exactly the certificate the server made, so verification stays on.
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};
await using CurlClient client = CurlClient.Create();

// The build: push build 41 under 1.0, and keep the manifest's digest for the deploy.
string pushed = await Build("app build 41\n", "1.0");
Console.WriteLine($"build pushed 1.0: {pushed}");
if (mode is "moved" or "pinned")
{
    Console.WriteLine($"rebuild pushed 1.0: {await Build("app build 42\n", "1.0")}");
}

if (mode == "changed")
{
    // Other bytes of the same length over build 41's layer, as the store's owner, not a client.
    await server.Files.PutAsync($"/demo/app/blobs/sha256/{Hex("app build 41\n"u8.ToArray())}", "app build 42\n"u8.ToArray());
    Console.WriteLine("layer overwritten in the store");
}

string reference = mode switch
{
    "pinned" or "changed" => $"@{pushed}",
    "unknown" => ":2.0",
    _ => ":1.0",
};
try
{
    return await Deploy(reference, pushed, mode == "password" ? "wrong" : "d3ploy");
}
catch (CurlException failure)
{
    Console.WriteLine($"deploy failed: {failure.Code} ({(int)failure.Code}) {failure.Message}");
    return 1;
}

// Pushes the empty config, one layer and the manifest that names them under the tag; answers
// the manifest's digest.
async Task<string> Build(string layerText, string tag)
{
    byte[] config = "{}"u8.ToArray();
    byte[] layer = Encoding.ASCII.GetBytes(layerText);
    byte[] manifest = Encoding.ASCII.GetBytes(
        "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\","
        + $"\"config\":{{\"mediaType\":\"application/vnd.oci.empty.v1+json\",\"digest\":\"sha256:{Hex(config)}\",\"size\":{config.Length}}},"
        + $"\"layers\":[{{\"mediaType\":\"text/plain\",\"digest\":\"sha256:{Hex(layer)}\",\"size\":{layer.Length}}}]}}\n");
    foreach ((string target, byte[] bytes) in new[] { ($"@sha256:{Hex(config)}", config), ($"@sha256:{Hex(layer)}", layer), (":" + tag, manifest) })
    {
        CurlResponse response = await client.SendAsync(Request(HttpMethod.Put, repo + target, "build", "b1ld") with { Body = CurlContent.FromBytes(bytes) }, CancellationToken.None);
        await response.Body.DisposeAsync();
    }

    return $"sha256:{Hex(manifest)}";
}

// Pulls the manifest at the reference, checks it against the digest the build pushed, then pulls
// and checks each blob it names. 0 when the image is the one the build pushed.
async Task<int> Deploy(string reference, string expected, string password)
{
    (CurlResponse response, byte[] manifest) = await Pull(repo + reference, password);
    string got = $"sha256:{Hex(manifest)}";
    string said = response.Headers.First(h => h.Key.Equals("Docker-Content-Digest", StringComparison.OrdinalIgnoreCase)).Value;
    Console.WriteLine($"deploy pulled {reference}: {got}, the registry says {said}");
    if (got != expected)
    {
        Console.WriteLine($"FAIL: not the manifest the build pushed ({expected})");
        return 1;
    }

    using JsonDocument parsed = JsonDocument.Parse(manifest);
    var blobs = new[] { parsed.RootElement.GetProperty("config") }.Concat(parsed.RootElement.GetProperty("layers").EnumerateArray());
    foreach (JsonElement descriptor in blobs)
    {
        string digest = descriptor.GetProperty("digest").GetString()!;
        (_, byte[] bytes) = await Pull($"{repo}@{digest}", password);
        bool ok = $"sha256:{Hex(bytes)}" == digest && bytes.Length == descriptor.GetProperty("size").GetInt32();
        Console.WriteLine($"blob {digest[..19]}: {bytes.Length} bytes, {(ok ? "hash ok" : "HASH DIFFERS")}");
        if (!ok)
        {
            return 1;
        }
    }

    Console.WriteLine("PASS: the deployed image is the one the build pushed");
    return 0;
}

async Task<(CurlResponse Response, byte[] Bytes)> Pull(string url, string password)
{
    CurlResponse response = await client.SendAsync(Request(HttpMethod.Get, url, "deploy", password), CancellationToken.None);
    await using Stream body = response.Body;
    using var copy = new MemoryStream();
    await body.CopyToAsync(copy);
    return (response, copy.ToArray());
}

CurlRequest Request(HttpMethod method, string url, string user, string password) =>
    new(method, url) { Tls = trust, Authentication = new() { UserName = user, Password = password } };

static string Hex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
