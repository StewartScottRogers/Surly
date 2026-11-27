// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- 1.3`. Package versions are the book's pins in
// Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Tls;
using Surl.Api;

// "1.3" asks the server for TLS 1.3 only, "1.2" leaves surl's default, and "empty" asks for an
// impossible range: nothing below 1.3 and nothing above 1.2.
string lowest = args[0];

// The server, in-process, with the certificate and key that openssl wrote (--cert, --key).
// There is no property for the version range, so it goes in as surl's own option: --tlsv1.3.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
if (lowest != "1.2") options.AdditionalArguments.Add("--tlsv1.3");
if (lowest == "empty") { options.AdditionalArguments.Add("--tls-max"); options.AdditionalArguments.Add("1.2"); }
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"server refused to start: {e.Message}");
    return 2;
}
await using var _ = server;

// Two clients that trust the certificate: one that stops at TLS 1.2 (curl --tls-max 1.2),
// and one that wants TLS 1.3 or later (curl --tlsv1.3).
await Fetch("TLS 1.2 client", SslVersion.Default, SslVersionMaximum.Tls12);
await Fetch("TLS 1.3 client", SslVersion.Tls13, SslVersionMaximum.None);
return 0;

async Task Fetch(string who, SslVersion min, SslVersionMaximum max)
{
    var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem", SslVersion = min, SslVersionMaximum = max };
    await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });
    try
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
        await using Stream body = response.Body;
        Console.WriteLine($"{who}: {(int)response.StatusCode} {(await new StreamReader(body).ReadToEndAsync()).Trim()}");
    }
    catch (CurlException e)
    {
        Console.WriteLine($"{who}: refused, curl code {(int)e.Code} {e.Code}");
    }
}
