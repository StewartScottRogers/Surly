// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Tests a client's login flow against an in-process HTTPS server: Basic to fetch a token,
// then the API with that token as a Bearer token; then reads back the logins the server judged.
// Run: dotnet run token-flow.cs [-- <password> [<token>]]
//   (no argument)  the right password, and the token the login hands back
//   <password>     log in with this password instead
//   <token>        call the API with this token instead of the one handed back
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string password = args.Length > 0 ? args[0] : "s3cret";
string? tokenOverride = args.Length > 1 ? args[1] : null;

// The server: one password account, one Bearer token (the empty user name), the token as a file.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("app", "s3cret"), new SurlAccount("", "tok-7f3a9c") },
    RecordExchanges = true,
};
options.Files["token.txt"] = "tok-7f3a9c\n"u8.ToArray();
options.Files["api/orders.json"] = "[{\"id\":1,\"total\":40}]\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The client trusts exactly the certificate the server made, so verification stays on.
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};
await using CurlClient client = CurlClient.Create();
int exitCode = 0;

// Step 1: Basic login; the body is the token.
(int status, string body) = await Get("token.txt", new CurlAuthenticationOptions
{
    UserName = "app", Password = password, HttpAuthentication = HttpAuthenticationMethods.Basic,
});
Console.WriteLine($"login: {status}");
if (status != 200)
{
    exitCode = 1;
}
else
{
    // Step 2: the API, with the token as a Bearer token.
    string token = tokenOverride ?? body.TrimEnd();
    (status, body) = await Get("api/orders.json", new CurlAuthenticationOptions
    {
        XOAuth2Bearer = token, HttpAuthentication = HttpAuthenticationMethods.Bearer,
    });
    Console.WriteLine($"api: {status} {body.TrimEnd()}".TrimEnd());
    exitCode = status == 200 ? 0 : 1;
}

// What the server recorded: every login it judged, in order.
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"recorded: {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

return exitCode;

// One GET over HTTPS with the given login: the status and the body.
async Task<(int, string)> Get(string path, CurlAuthenticationOptions login)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}") { Tls = trust, Authentication = login };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream stream = response.Body;
    return (response.StatusCode, response.StatusCode == 200 ? await new StreamReader(stream).ReadToEndAsync() : "");
}
