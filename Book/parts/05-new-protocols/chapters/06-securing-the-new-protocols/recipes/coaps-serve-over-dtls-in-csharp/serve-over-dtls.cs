// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a sensor's configuration over CoAP inside DTLS with Surl.Api, and reads it with Curl.Api.
// Run: dotnet run serve-over-dtls.cs -- <case>
//   coaps        coaps with the certificate, no account (the default)
//   untrusted    the same, the certificate not trusted
//   plain-port   plain coap sent to the coaps port
//   account      an account: read over plain coap      password   the same, with a user name and password
//   account-dtls an account: read over coaps            anonymous  an account and AllowAnonymous, plain coap
//   psk          an account and no certificate, coaps   nothing    coaps with no certificate and no account
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "coaps";
bool plain = which is "account" or "password" or "anonymous";

// The server: one resource, on a free UDP port.
var options = new SurlServerOptions
{
    ListenUrls = { plain ? "coap://127.0.0.1:0" : "coaps://127.0.0.1:0" },
    AllowAnonymous = which == "anonymous",
};
options.Files["sensor.json"] = "{\"interval\":60}\n"u8.ToArray();
if (which is not ("psk" or "nothing" or "anonymous" or "account" or "password"))
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

if (which is "account" or "password" or "account-dtls" or "anonymous" or "psk")
{
    options.Accounts.Add(new SurlAccount("alice", "secret"));
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"start refused: {e.ExitCode} ({(int)e.ExitCode})");
    Console.WriteLine(e.Message);
    return (int)e.ExitCode;
}

await using (server)
{
    // The client: trusts server.pem as its own authority, and gives up after 10 seconds.
    ReadOnlyMemory<byte>? authority = File.ReadAllBytes("server.pem");
    await using CurlClient client = CurlClient.Create(new CurlClientOptions
    {
        Tls = which == "untrusted" ? null : new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = authority } },
        Authentication = which == "password" ? new CurlAuthenticationOptions { UserName = "alice", Password = "secret" } : null,
        Timeout = TimeSpan.FromSeconds(10),
    });
    string url = $"{server.BoundUrl}sensor.json";
    if (which == "plain-port")
    {
        url = url.Replace("coaps://", "coap://");
    }

    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
        await using Stream body = response.Body;
        // CoAP has no HTTP status: the response code is the first header line, and StatusCode is 0.
        Console.WriteLine($"{response.Headers.First().Key} (StatusCode {response.StatusCode})");
        Console.Write(await new StreamReader(body).ReadToEndAsync());
        return 0;
    }
    catch (CurlException e)
    {
        Console.WriteLine($"refused: {e.Code} ({(int)e.Code})");
        return (int)e.Code;
    }
}
