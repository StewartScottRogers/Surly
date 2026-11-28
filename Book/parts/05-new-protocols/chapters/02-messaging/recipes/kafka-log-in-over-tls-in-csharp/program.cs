// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves Kafka with one account over TLS or without it, using Surl.Api, and logs in to it with Curl.Api.
// Run: dotnet run program.cs -- <case>
//   tls            kafkas, the right password, the certificate trusted
//   tls-wrong      kafkas, a wrong password
//   tls-no-login   kafkas, no login
//   tls-untrusted  kafkas, the right password, the certificate not trusted
//   plain          kafka, the right password
//   plain-wrong    kafka, a wrong password
//   scram-256      kafka offering SCRAM-SHA-256 only, a wrong password
//   only-plain     kafka told to offer PLAIN only, the right password
//   loosened       kafka with AllowPlaintextAuthentication, a wrong password
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Tls;
using Surl.Api;

string which = args[0];
bool tls = which.StartsWith("tls");

// The broker: one account and no loosening option unless the case names it. Its certificate and key are the
// files openssl wrote. AuthenticationMethods is --auth: it replaces what the listener offers.
var options = new SurlServerOptions
{
    ListenUrls = { tls ? "kafkas://127.0.0.1:0" : "kafka://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
};
if (tls)
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

if (which == "scram-256") options.AuthenticationMethods = ["SCRAM-SHA-256"];
if (which == "only-plain") options.AuthenticationMethods = ["PLAIN"];
if (which == "loosened") options.AllowPlaintextAuthentication = true;

await using SurlServer server = await SurlServer.StartAsync(options);

// The record the account is to read: the server's own produce, since a Curl.Api POST does not produce.
await server.Messages.PublishAsync("orders", Encoding.UTF8.GetBytes("first"));

// The client: -u is Authentication, --cacert is CertificateAuthorityFile.
var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}orders");
if (which != "tls-no-login")
{
    request = request with
    {
        Authentication = new CurlAuthenticationOptions
        {
            UserName = "alice",
            Password = which is "tls-wrong" or "plain-wrong" or "scram-256" or "loosened" ? "wrong" : "s3cret",
        },
    };
}

var tlsOptions = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityFile = which == "tls-untrusted" ? null : "server.pem" } };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = tlsOptions });
try
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    Console.WriteLine($"logged in, consumed: {text.TrimEnd()}");
    return 0;
}
catch (CurlException e)
{
    // The message of a login refusal is the broker's; a failed handshake's is the TLS library's, which differs by platform.
    Console.WriteLine($"refused: curl code {(int)e.Code} {e.Code}{((int)e.Code == 67 ? $": {e.Message}" : "")}");
    return (int)e.Code;
}
