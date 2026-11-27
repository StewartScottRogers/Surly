// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Logs in to an in-process surl over AMQP and AMQPS with Curl.Api: PLAIN is refused over plain AMQP, which offers
// only CRAM-MD5, and accepted over AMQPS.
// Run: dotnet run program.cs [-- case]
//   (no argument)            alice logs in over AMQPS, trusting the certificate, and reads a message
//   plain-refused            the same login over plain AMQP on the same server
//   plaintext-allowed        plain AMQP on a server given AllowPlaintextAuthentication
//   wrong-password           a wrong password over AMQPS
//   unknown-user             a user name with no account over AMQPS
//   no-login                 no login at all over AMQPS
//   plaintext-wrong-password a wrong password over plain AMQP with AllowPlaintextAuthentication
//   untrusted-certificate    AMQPS with no certificate to trust
//   needs-a-certificate      start an amqps listener with no certificate
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "login";

// One server, two listeners: AMQPS with the certificate that setup made, and plain AMQP. One account and, unless a
// case asks for it, no loosening: AllowPlaintextAuthentication is --allow-plaintext-auth.
var options = new SurlServerOptions
{
    ListenUrls = { "amqps://127.0.0.1:0", "amqp://127.0.0.1:0" },
    CertificateFile = which == "needs-a-certificate" ? null : "server.pem",
    KeyFile = which == "needs-a-certificate" ? null : "server.key",
    AllowPlaintextAuthentication = which is "plaintext-allowed" or "plaintext-wrong-password",
};
options.Accounts.Add(new SurlAccount("alice", "s3cret"));
options.Queues.Add("orders");

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    // The same refusal surl makes: exit 58, a listener of that scheme with no certificate.
    Console.WriteLine($"start refused: {(int)refused.ExitCode} {refused.ExitCode}");
    return 1;
}

await using SurlServer running = server;
Uri amqps = server.BoundUrls[0];
Uri amqp = server.BoundUrls[1];

// The client trusts the certificate setup made, except in the case that leaves it out (curl --cacert).
CurlClientOptions clientOptions = which == "untrusted-certificate"
    ? new CurlClientOptions()
    : new CurlClientOptions { Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" } } };
await using CurlClient client = CurlClient.Create(clientOptions);

// Curl.Api does not publish over AMQP, so the message is put on the queue from the server's side.
await server.Messages.EnqueueAsync("orders", Encoding.UTF8.GetBytes("paid"));

switch (which)
{
    case "plain-refused" or "plaintext-allowed":
        Console.WriteLine(await Receive(amqp, "alice", "s3cret"));
        break;
    case "plaintext-wrong-password":
        Console.WriteLine(await Receive(amqp, "alice", "wrong"));
        break;
    case "wrong-password":
        Console.WriteLine(await Receive(amqps, "alice", "wrong"));
        break;
    case "unknown-user":
        Console.WriteLine(await Receive(amqps, "bob", "s3cret"));
        break;
    case "no-login":
        Console.WriteLine(await Receive(amqps, null, null));
        break;
    default:
        Console.WriteLine(await Receive(amqps, "alice", "s3cret"));
        break;
}

return 0;

// A GET takes one message and answers its body. AMQP 0-9-1 names a virtual host and then the queue: %2F is the
// virtual host /. A refusal is a CurlException with curl's own code, not a status.
async Task<string> Receive(Uri listener, string? user, string? password)
{
    var request = new CurlRequest(HttpMethod.Get, $"{listener}%2F/orders")
    {
        Authentication = user is null ? null : new CurlAuthenticationOptions { UserName = user, Password = password },
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        return $"read: {await new StreamReader(body).ReadToEndAsync()}";
    }
    catch (CurlException failed)
    {
        // Only a login refusal has words of surl's; a certificate refusal is the TLS library's, so the code is enough.
        return $"refused: {(int)failed.Code} {failed.Code}" + ((int)failed.Code == 67 ? $": {failed.Message}" : "");
    }
}
