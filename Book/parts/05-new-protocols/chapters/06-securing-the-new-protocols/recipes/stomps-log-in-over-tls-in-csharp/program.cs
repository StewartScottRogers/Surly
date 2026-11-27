// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- stomps alice s3cret`. Package versions are the
// book's pins in Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// The scheme to listen on, stomps (TLS) or stomp (plain); the user name and password to log in with,
// or "none" for no login; "plaintext" to loosen the server; "untrusted" to leave the certificate untrusted.
string scheme = args[0];
string user = args[1];
string password = args[2];
bool plaintext = args.Contains("plaintext");
bool untrusted = args.Contains("untrusted");

// The server, in-process: one account and one queue. AllowPlaintextAuthentication is --allow-plaintext-auth.
var options = new SurlServerOptions
{
    ListenUrls = { $"{scheme}://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    AllowPlaintextAuthentication = plaintext,
    RecordExchanges = true,
};
options.Queues.Add("jobs");
if (scheme == "stomps")
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

await using var server = await SurlServer.StartAsync(options);

// The client: a login, the certificate to trust (curl -u and --cacert) and a message to send (-d).
var request = new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}queue/jobs")
{
    Body = CurlContent.FromBytes("hello"u8.ToArray()),
    Authentication = user == "none" ? null : new CurlAuthenticationOptions { UserName = user, Password = password },
};
var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = untrusted ? null : "server.pem" };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });
try
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await response.Body.DisposeAsync();
}
catch (CurlException e)
{
    Console.WriteLine($"refused: curl code {(int)e.Code} {e.Code}: {e.Message}");
    return (int)e.Code;
}

// The send is not acknowledged, so the call can return before the server has applied it: wait, for at
// most 10 s, for the end of the client's exchange, which the server records after applying it.
await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = 0 }, TimeSpan.FromSeconds(10));

// The server holds the message, so it can say what the login let through.
SurlQueuePage held = await server.Messages.BrowseQueueAsync("jobs", -1, 100, CancellationToken.None);
Console.WriteLine($"queue jobs holds {held.ReadyCount}: {string.Join(", ", held.Messages.Select(m => Encoding.UTF8.GetString(m.Payload.Span)))}");
return 0;
