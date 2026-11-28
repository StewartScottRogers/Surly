// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file over HTTPS behind a login with Surl.Api and fetches it with Curl.Api, in one process:
// first with a throwaway certificate the client cannot verify, then with one the program makes and trusts.
// Run: dotnet run login-over-tls.cs
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

var alice = new CurlAuthenticationOptions { UserName = "alice", Password = "secret" };
var wrong = new CurlAuthenticationOptions { UserName = "alice", Password = "wrong" };
var skipVerification = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false, VerifyHost = false } };

await using CurlClient client = CurlClient.Create();

// An https listen URL needs a certificate; with none, the server refuses to start.
try
{
    await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "https://127.0.0.1:0" } });
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"no certificate: {refused.Lines[0]}");
}

// 1. A throwaway certificate and one account.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("alice", "secret") },
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using (var server = await SurlServer.StartAsync(options))
{
    string https = $"{server.BoundUrl}hello.txt";
    Console.WriteLine($"self-signed: {server.BoundUrl}");
    await Fetch("verified, alice", https, alice, tls: null);
    await Fetch("unverified, no login", https, login: null, skipVerification);
    await Fetch("unverified, wrong password", https, wrong, skipVerification);
    await Fetch("unverified, alice", https, alice, skipVerification);
}

// 2. A certificate the program makes, given to the server as files and to the client as trusted.
DirectoryInfo dir = Directory.CreateTempSubdirectory();
string certificatePem = MakeCertificate(Path.Combine(dir.FullName, "server.pem"), Path.Combine(dir.FullName, "server.key"));
var own = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    CertificateFile = Path.Combine(dir.FullName, "server.pem"),
    KeyFile = Path.Combine(dir.FullName, "server.key"),
    Accounts = { new SurlAccount("alice", "secret") },
};
own.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using (var server = await SurlServer.StartAsync(own))
{
    Console.WriteLine($"own certificate: {server.BoundUrl}");
    var trust = new CurlTlsOptions
    {
        Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(certificatePem) },
    };
    await Fetch("trusted, alice", $"{server.BoundUrl}hello.txt", alice, trust);
}

dir.Delete(recursive: true);
return 0;

// One GET; prints the status and the body, or the curl error code the client threw.
async Task Fetch(string label, string url, CurlAuthenticationOptions? login, CurlTlsOptions? tls)
{
    var request = new CurlRequest(HttpMethod.Get, url);
    if (login is not null)
    {
        request = request with { Authentication = login };
    }

    if (tls is not null)
    {
        request = request with { Tls = tls };
    }

    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        Console.WriteLine($"{label}: {response.StatusCode} {text}".TrimEnd());
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}");
    }
}

// A certificate for 127.0.0.1, signed by its own key, valid for 30 days; writes both as PEM.
static string MakeCertificate(string certificateFile, string keyFile)
{
    using RSA key = RSA.Create(2048);
    var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var names = new SubjectAlternativeNameBuilder();
    names.AddIpAddress(IPAddress.Loopback);
    request.CertificateExtensions.Add(names.Build());
    using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30));
    string pem = certificate.ExportCertificatePem() + "\n";
    File.WriteAllText(certificateFile, pem);
    File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem() + "\n");
    return pem;
}
