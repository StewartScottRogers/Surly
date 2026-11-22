// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs -- signalrs alice s3cret`. Package versions are the
// book's pins in Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text.Json;
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

// A SignalR client starts with a negotiate request, an HTTP POST, and the login is judged there. The hub
// is HTTP under its own scheme, so the program asks for it as http or https.
string address = server.BoundUrl.ToString().Replace(scheme + "://", scheme == "signalrs" ? "https://" : "http://");
var request = new CurlRequest(HttpMethod.Post, $"{address}negotiate?negotiateVersion=1")
{
    Authentication = user == "none" ? null : new CurlAuthenticationOptions { UserName = user, Password = password },
};
var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = untrusted ? null : "server.pem" };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });
try
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    Console.WriteLine($"negotiate: {response.StatusCode}");
    if (response.StatusCode == 200)
    {
        bool token = JsonDocument.Parse(text).RootElement.TryGetProperty("connectionToken", out _);
        Console.WriteLine($"connection token: {token}");
    }
}
catch (CurlException e)
{
    Console.WriteLine($"refused: curl code {(int)e.Code} {e.Code}");
    return (int)e.Code;
}

return 0;
