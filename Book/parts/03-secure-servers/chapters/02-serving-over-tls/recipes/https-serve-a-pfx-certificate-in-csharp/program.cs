// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- s3cret`. Package versions are the book's pins in
// Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Tls;
using Surl.Api;

// The passphrase to give the PFX file: the right one, or a wrong one.
string passphrase = args[0];

// The server, in-process, serving the certificate and key inside server.pfx.
// CertificateFile is --cert, KeyPassphrase is --pass. There is no property for the file's
// format, so it goes in as surl's own option: --cert-type P12.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    CertificateFile = "server.pfx",
    KeyPassphrase = passphrase,
};
options.AdditionalArguments.Add("--cert-type");
options.AdditionalArguments.Add("P12");
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"server refused to start: {e.Message}");
    return 58;
}

await using var _ = server;

// The client trusts server.pem, the same certificate as a PEM file (curl --cacert).
var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });
CurlResponse response = await client.SendAsync(
    new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
await using Stream body = response.Body;
Console.Write(await new StreamReader(body).ReadToEndAsync());
return 0;
