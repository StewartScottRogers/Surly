// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- socketios alice s3cret`. Package versions are the
// book's pins in Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Protocol.Abstractions;
using Surl.Api;

// The scheme to listen on, socketios (TLS) or socketio (plain); the user name and password to log in
// with, or "none" for no login; "plaintext" to loosen the server; "untrusted" to leave the certificate
// untrusted; "token" to serve a token account (:tok123).
string scheme = args[0];
string user = args[1];
string password = args[2];
bool plaintext = args.Contains("plaintext");
bool untrusted = args.Contains("untrusted");
bool token = args.Contains("token");

// The server, in-process: one account. AllowPlaintextAuthentication is --allow-plaintext-auth.
var options = new SurlServerOptions
{
    ListenUrls = { $"{scheme}://127.0.0.1:0" },
    Accounts = { token ? new SurlAccount("", "tok123") : new SurlAccount("alice", "s3cret") },
    AllowPlaintextAuthentication = plaintext,
    RecordExchanges = true,
};
if (scheme == "socketios")
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

await using var server = await SurlServer.StartAsync(options);

// The client: a login (curl -u), the certificate to trust (--cacert) and one event to emit (-d). The
// call ends at the first event the server sends back; Timeout (curl -m 3) bounds it all the same.
var request = new CurlRequest(HttpMethod.Post, server.BoundUrl.ToString())
{
    Body = CurlContent.FromBytes("[\"chat\",\"hi\"]"u8.ToArray()),
    Authentication = user == "none" ? null : new CurlAuthenticationOptions { UserName = user, Password = password == "" ? null : password },
};
var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = untrusted ? null : "server.pem" };
await using CurlClient client = CurlClient.Create(new CurlClientOptions
{
    Tls = new CurlTlsOptions { Origin = tls },
    Timeout = TimeSpan.FromSeconds(3),
});
try
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string events = (await new StreamReader(body).ReadToEndAsync()).TrimEnd();

    // The join was accepted and the event came back. Ask the server what it judged of the login.
    var judged = (LoginJudgedEvent)await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.LoginJudged, AfterSequence = 0 }, TimeSpan.FromSeconds(10));
    Console.WriteLine($"joined: {events}; login accepted {judged.IsAccepted}");
    return 0;
}
catch (CurlException e)
{
    Console.WriteLine($"refused: curl code {(int)e.Code} {e.Code}: {e.Message}");
    return (int)e.Code;
}
