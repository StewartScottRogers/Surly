// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test zone served over DNS over TLS by an in-process surl; the client looks a service name up over
// DNS over TLS with Curl.Api, then calls the service at the address the answer gave.
// Run: dotnet run call-by-name.cs [-- case]
//   (no argument)  look up api.example.test and call it
//   not-in-zone    look up old-api.example.test, a name the zone does not hold
//   untrusted      look up without trusting the server's certificate
//   no-certificate start the DNS over TLS listener with no certificate
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using System.Text.Json.Nodes;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "call";

// DNS over TLS alone on one free port, the service on another; a throwaway certificate for the TLS.
var options = new SurlServerOptions
{
    ListenUrls = { "dns://127.0.0.1:0", "http://127.0.0.1:0" },
    SelfSigned = which != "no-certificate",
    AdditionalArguments = { "--dns-transport", "tls" },
};
options.Files["status.txt"] = "orders api: up\n"u8.ToArray();
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"start refused: exit {(int)refused.ExitCode} ({refused.ExitCode})");
    return 1;
}

await using (server)
{
    await server.Zones.AddZoneAsync("example.test", """
        $ORIGIN example.test.
        $TTL 300
        @   IN SOA ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300
        @   IN NS  ns1.example.test.
        api IN A   127.0.0.1
        """);
    string dns = server.BoundUrls[0].ToString();
    int httpPort = server.BoundUrls[1].Port;

    // The client trusts exactly the certificate the server made, so verification stays on.
    var trust = new CurlTlsOptions
    {
        Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
    };
    await using CurlClient client = CurlClient.Create();
    string name = which == "not-in-zone" ? "old-api.example.test" : "api.example.test";

    // The lookup: a dns:// request over TLS, its answer as JSON.
    var lookup = new CurlRequest(HttpMethod.Get, $"{dns}{name}")
    {
        Dns = new CurlDnsOptions { Transport = "tls", Json = true },
        Tls = which == "untrusted" ? null : trust,
    };
    JsonNode answer;
    try
    {
        CurlResponse looked = await client.SendAsync(lookup, CancellationToken.None);
        await using Stream body = looked.Body;
        answer = JsonNode.Parse(await new StreamReader(body).ReadToEndAsync())!;
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"lookup failed: curl error {(int)failed.Code}");
        return 1;
    }

    string? address = answer["answer"]!.AsArray()
        .Where(r => (string?)r!["type"] == "A").Select(r => (string?)r!["data"]!["address"]).FirstOrDefault();
    Console.WriteLine($"{name}: {answer["status"]}, {address ?? "no address"}");
    if (address is null)
    {
        return 1;
    }

    // The call: the URL keeps the name; Resolve connects that name and port to the address.
    var call = new CurlRequest(HttpMethod.Get, $"http://{name}:{httpPort}/status.txt")
    {
        Network = new CurlNetworkOptions { Resolve = [$"{name}:{httpPort}:{address}"] },
    };
    CurlResponse response = await client.SendAsync(call, CancellationToken.None);
    await using Stream status = response.Body;
    Console.Write($"{response.StatusCode} {await new StreamReader(status).ReadToEndAsync()}");
    return 0;
}
