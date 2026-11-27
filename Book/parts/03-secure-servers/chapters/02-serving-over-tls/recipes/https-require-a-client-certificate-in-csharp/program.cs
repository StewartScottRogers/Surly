// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A server that requires a client certificate, and a client that presents one (or not).
// Run: dotnet run program.cs -- <authority> <client>
//   authority  ca.pem (the test authority), none (ask for no certificate), or another file
//   client     p12 (client.p12), pem (client.pem and client.key), stranger (stranger.p12), or none
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Tls;
using Surl.Api;

string authority = args[0], client = args[1];

// The server: its own certificate and key (--cert, --key), and CaCertificateFile (--cacert),
// the authority a client certificate must come from. The log goes to a StringWriter.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    CaCertificateFile = authority == "none" ? null : authority,
    Log = TextWriter.Synchronized(log),
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"server refused to start: {(int)e.ExitCode} {e.ExitCode}");
    Console.WriteLine(e.Lines[0]);
    return (int)e.ExitCode;
}

await using var _ = server;

// The client trusts server.pem (curl --cacert) and presents the chosen certificate (curl --cert).
var tls = client switch
{
    "p12" => new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem", Certificate = "client.p12", CertificateType = "P12", KeyPassword = "s3cret" },
    "stranger" => new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem", Certificate = "stranger.p12", CertificateType = "P12", KeyPassword = "s3cret" },
    "pem" => new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem", Certificate = "client.pem", Key = "client.key" },
    _ => new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" },
};
await using CurlClient curl = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls }, Timeout = TimeSpan.FromSeconds(10) });
try
{
    CurlResponse response = await curl.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine((int)response.StatusCode);
    Console.Write(await new StreamReader(body).ReadToEndAsync());
    return 0;
}
catch (CurlException e)
{
    Console.WriteLine($"client failed: {e.Code}");
}

// What the server logged about the handshake, once it has written it.
for (int i = 0; i < 50 && !log.ToString().Contains("TLS handshake failed"); i++)
{
    await Task.Delay(100);
}

foreach (string line in log.ToString().Split('\n').Where(l => l.Contains("TLS handshake failed")))
{
    Console.WriteLine("server: " + line[(line.IndexOf("* ") + 2)..].TrimEnd('\r'));
}

return 1;
