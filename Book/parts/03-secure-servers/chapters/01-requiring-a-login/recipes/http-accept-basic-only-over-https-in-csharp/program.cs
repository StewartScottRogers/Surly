// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves HTTPS and plain HTTP with Basic as the only method, and tries both with Curl.Api, in one process.
// Run: dotnet run program.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;

// One server, two listeners, one account, Basic the only method on offer.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0", "http://127.0.0.1:0" },
    SelfSigned = true,
    AuthenticationMethods = ["basic"],
    Accounts = { new SurlAccount("alice", "s3cret") },
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
string https = server.BoundUrls[0] + "hello.txt";
string http = server.BoundUrls[1] + "hello.txt";

// The client trusts exactly the certificate the server made, so TLS verification stays on.
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};
await using CurlClient client = CurlClient.Create();

await Show("https, right password", https, "alice", "s3cret", tls: trust);
await Show("https, wrong password", https, "alice", "guess", tls: trust);
await Show("https, no login", https, null, null, tls: trust);
await Show("http, right password", http, "alice", "s3cret", tls: null);
await Show("http, wrong password", http, "alice", "guess", tls: null);
await Show("http, no login", http, null, null, tls: null);
return 0;

// One GET: prints the status, the WWW-Authenticate headers the answer carries, and the body if it was 200
// (a refusal's body is one line naming its status).
async Task Show(string label, string url, string? user, string? password, CurlTlsOptions? tls)
{
    var request = new CurlRequest(HttpMethod.Get, url);
    if (user is not null)
    {
        request = request with { Authentication = new CurlAuthenticationOptions { UserName = user, Password = password } };
    }

    if (tls is not null)
    {
        request = request with { Tls = tls };
    }

    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    string offered = string.Join("; ", response.Headers.Where(h => h.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value));
    Console.WriteLine($"{label}: {response.StatusCode} [{offered}] {(response.StatusCode == 200 ? text : "")}".TrimEnd());
}
