// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- signalrs alice s3cret`. Package versions are the
// book's pins in Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// The scheme to listen on, signalrs (TLS) or signalr (plain); the user name and password to log in
// with, or "none" for no login; "plaintext" to loosen the server; "untrusted" to leave the certificate untrusted.
string scheme = args[0];
string user = args[1];
string password = args[2];
bool plaintext = args.Contains("plaintext");
bool untrusted = args.Contains("untrusted");

// The server, in-process: one account. AllowPlaintextAuthentication is --allow-plaintext-auth.
var options = new SurlServerOptions
{
    ListenUrls = { $"{scheme}://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    AllowPlaintextAuthentication = plaintext,
};
if (scheme == "signalrs")
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

await using var server = await SurlServer.StartAsync(options);

// The Body is one hub invocation, sent as curl -d sends it: Curl.Api negotiates with the login,
// opens the hub's WebSocket and answers the completion's result.
var request = new CurlRequest(HttpMethod.Post, server.BoundUrl.ToString())
{
    Authentication = user == "none" ? null : new CurlAuthenticationOptions { UserName = user, Password = password },
    Body = """{"type":1,"target":"Echo","arguments":["hi"]}"""u8.ToArray(),
};
var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = untrusted ? null : "server.pem" };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
    CurlResponse response = await client.SendAsync(request, timeout.Token);
    await using Stream body = response.Body;
    Console.Write($"Echo: {await new StreamReader(body).ReadToEndAsync()}");
}
catch (CurlException e)
{
    Console.WriteLine($"refused: curl code {(int)e.Code} {e.Code}: {e.Message}");
    return (int)e.Code;
}

return 0;
