// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A deploy tool's tag listing, tested against an in-process registry: it logs in at the token
// endpoint with Basic for a token scoped to pull the repository, then asks for the tag list with
// the token as a Bearer credential.
// Run: dotnet run list-tags.cs [-- <case>]
//   (no argument)  a token, then the tag list
//   no-token       the tag list with no token
//   password       the tag list with the password sent as Basic
//   wrong          a wrong password at the token endpoint
//   scope          a token scoped to another repository
//   pages          the tag list two at a time, following the Link header
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using System.Text.Json;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "";

// The registry: TLS with a certificate it makes, one account, and the repository demo/app seeded
// as an OCI image layout in memory, one manifest under three tags.
const string Manifest = "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"config\":{\"mediaType\":\"application/vnd.oci.empty.v1+json\",\"digest\":\"sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a\",\"size\":2},\"layers\":[{\"mediaType\":\"text/plain\",\"digest\":\"sha256:33984613b0129bf0f7350dad5e7e372ccbfd585857d4cc9da2f1e333ac5b7603\",\"size\":13}]}\n";
const string Digest = "sha256:267a1a95fbd5b9ea80c16d403cfb6c46dfa46b3abc615dafa13acb32e452c275";
var options = new SurlServerOptions
{
    ListenUrls = { "oci://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("alice", "s3cret") },
};
options.Files["demo/app/oci-layout"] = "{\"imageLayoutVersion\":\"1.0.0\"}"u8.ToArray();
options.Files[$"demo/app/blobs/sha256/{Digest[7..]}"] = Encoding.ASCII.GetBytes(Manifest);
options.Files["demo/app/index.json"] = Encoding.ASCII.GetBytes(
    "{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":["
    + string.Join(",", new[] { "latest", "1.0", "1.1" }.Select(tag =>
        $"{{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"digest\":\"{Digest}\",\"size\":359,\"annotations\":{{\"org.opencontainers.image.ref.name\":\"{tag}\"}}}}"))
    + "]}");
await using SurlServer server = await SurlServer.StartAsync(options);
string registry = $"https://127.0.0.1:{server.BoundUrl.Port}";

// The client trusts exactly the certificate the server made, so verification stays on.
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};
await using CurlClient client = CurlClient.Create();
var basic = new CurlAuthenticationOptions { UserName = "alice", Password = mode == "wrong" ? "wrong" : "s3cret", HttpAuthentication = HttpAuthenticationMethods.Basic };

switch (mode)
{
    case "no-token":
        (CurlResponse bare, _) = await Get("/v2/demo/app/tags/list", null);
        Console.WriteLine($"{bare.StatusCode} {Header(bare, "WWW-Authenticate")}");
        return 0;
    case "password":
        Console.WriteLine($"{(await Get("/v2/demo/app/tags/list", basic)).Response.StatusCode}");
        return 0;
}

// The token: a Basic login at the token endpoint, for a token scoped to pull the repository.
string scope = mode == "scope" ? "repository:other/app:pull" : "repository:demo/app:pull";
(CurlResponse issued, string answer) = await Get($"/token?service=surl&scope={scope}", basic);
if (issued.StatusCode != 200)
{
    Console.WriteLine($"token endpoint: {issued.StatusCode}, no token");
    return 1;
}

string token;
using (JsonDocument json = JsonDocument.Parse(answer))
{
    token = json.RootElement.GetProperty("token").GetString()!;
    Console.WriteLine($"token endpoint: {issued.StatusCode}, a token good for {json.RootElement.GetProperty("expires_in")} s");
}

var bearer = new CurlAuthenticationOptions { XOAuth2Bearer = token, HttpAuthentication = HttpAuthenticationMethods.Bearer };

// The tag list with the token; with pages, two at a time, following each Link header.
string? next = mode == "pages" ? "/v2/demo/app/tags/list?n=2" : "/v2/demo/app/tags/list";
int status = 0;
while (next is not null)
{
    (CurlResponse listed, string body) = await Get(next, bearer);
    status = (int)listed.StatusCode;
    string? link = Header(listed, "Link");
    Console.WriteLine($"{status} {body}" + (link is null ? "" : $"\nLink: {link}"));
    next = link?.Split('<', '>')[1];
}

return status == 200 ? 0 : 1;

async Task<(CurlResponse Response, string Body)> Get(string path, CurlAuthenticationOptions? login)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, registry + path) { Tls = trust, Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return (response, await new StreamReader(body).ReadToEndAsync());
}

static string? Header(CurlResponse response, string name) =>
    response.Headers.Where(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).FirstOrDefault();
