// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test points its client at a test zone: an in-process surl serves the zone over DNS over HTTPS and
// the service the zone names; Curl.Api's DohUrl sends the client's lookups there.
// Run: dotnet run point-at-zone.cs [-- case]
//   (no argument)  call api.example.test, resolved from the test zone
//   not-in-zone    call old-api.example.test, a name the zone does not hold
//   no-doh         call api.example.test with the system's resolver
//   untrusted      call api.example.test without trusting the DNS server's certificate
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "call";

// DNS over HTTPS on one free port, the service on another; a throwaway certificate for the TLS.
var options = new SurlServerOptions
{
    ListenUrls = { "dns://127.0.0.1:0", "http://127.0.0.1:0" },
    SelfSigned = true,
    AdditionalArguments = { "--dns-transport", "https" },
};
options.Files["status.txt"] = "orders api: up\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The test zone: the orders service, api, at the loopback address.
await server.Zones.AddZoneAsync("example.test", """
    $ORIGIN example.test.
    $TTL 300
    @   IN SOA ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300
    @   IN NS  ns1.example.test.
    api IN A   127.0.0.1
    """);
string dohUrl = $"https://127.0.0.1:{server.BoundUrls[0].Port}/dns-query";
int httpPort = server.BoundUrls[1].Port;

// The client's settings for every request: lookups go to the test server, whose certificate it trusts.
var client = CurlClient.Create(new CurlClientOptions
{
    Network = which == "no-doh" ? null : new CurlNetworkOptions { DohUrl = dohUrl },
    Tls = which == "untrusted" ? null : new CurlTlsOptions
    {
        Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
    },
    Timeout = TimeSpan.FromSeconds(10),
});
await using (client)
{
    // The call the code under test makes: by name, as its configuration says.
    string name = which == "not-in-zone" ? "old-api.example.test" : "api.example.test";
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"http://{name}:{httpPort}/status.txt"), CancellationToken.None);
        await using Stream body = response.Body;
        Console.Write($"{response.StatusCode} {await new StreamReader(body).ReadToEndAsync()}");
        return 0;
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"{name}: curl error {(int)failed.Code} ({failed.Code})");
        return 1;
    }
}
