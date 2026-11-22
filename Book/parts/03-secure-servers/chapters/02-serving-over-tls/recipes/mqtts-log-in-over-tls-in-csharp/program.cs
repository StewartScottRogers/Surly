// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- mqtts s3cret`. Package versions are the book's
// pins in Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// Which scheme to listen on, mqtts (TLS) or mqtt (plain), and the password to log in with,
// or "none" to log in with no user name.
string scheme = args[0];
string password = args[1];

// The server, in-process: one account and no loosening option. --topic keeps what is published.
var options = new SurlServerOptions
{
    ListenUrls = { $"{scheme}://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    Topics = { "home/#;retain" },
    RecordExchanges = true,
};
if (scheme == "mqtts")
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

await using var server = await SurlServer.StartAsync(options);

// The client: a login, and for mqtts the certificate to trust (curl -u and --cacert).
var request = new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}home/temp")
{
    Body = CurlContent.FromBytes("21.5"u8.ToArray()),
    Authentication = new CurlAuthenticationOptions
    {
        UserName = password == "none" ? null : "alice",
        Password = password == "none" ? null : password,
    },
};
var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });
try
{
    await client.SendAsync(request, CancellationToken.None);
}
catch (CurlException e)
{
    Console.WriteLine($"refused: curl code {(int)e.Code} {e.Code}: {e.Message}");
    return 8;
}

// A QoS 0 publish is not acknowledged, so the call can return before the broker has kept it: wait,
// for at most 10 s, for the end of the client's exchange, which the broker records after applying it.
await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = 0 }, TimeSpan.FromSeconds(10));

// The server kept the message, so it can say what the login let through.
SurlRetainedMessage kept = await server.Messages.ReadRetainedAsync("home/temp", CancellationToken.None);
Console.WriteLine($"{kept.Topic} = {Encoding.UTF8.GetString(kept.Payload.Span)}");
return 0;
