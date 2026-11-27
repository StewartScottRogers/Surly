// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Tests a client that keeps a Bearer token between calls against an in-process registry: the
// test revokes every token between two calls, and the client must see 401, log in again for a new
// token and retry once. Then it waits for the logins the server recorded.
// Run: dotnet run token-session.cs [-- <mode>]
//   (no argument)  two calls with a revocation between them
//   steady         two calls, no revocation
//   broken         a fault answers every API call 401, even after a new login
//   guess          the client starts with a stale token and a wrong password
//   endpoint       what the token endpoint answers, and a call with no token
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using System.Text.Json;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "";

// The server: a registry over TLS, whose token endpoint issues a new token at every login.
var options = new SurlServerOptions
{
    ListenUrls = { "oci://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("app", "s3cret") },
    RecordExchanges = true,
};
if (mode == "broken")
{
    options.Faults.Add(SurlFault.ProtocolError("401 UNAUTHORIZED") with { Scheme = "oci", Path = "/v2/" });
}

await using SurlServer server = await SurlServer.StartAsync(options);
string baseUrl = $"https://127.0.0.1:{server.BoundUrl.Port}/";

// The client trusts exactly the certificate the server made, so verification stays on.
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};
await using CurlClient curl = CurlClient.Create();
var client = new TokenClient(curl, trust, baseUrl, "app", mode == "guess" ? "guess" : "s3cret")
{
    Token = mode == "guess" ? "tok-from-yesterday" : null,
};

if (mode == "endpoint")
{
    // What the token endpoint answers, and what a call with no token gets.
    using (JsonDocument answer = JsonDocument.Parse((await Get("token?service=surl", new CurlAuthenticationOptions
    {
        UserName = "app", Password = "s3cret", HttpAuthentication = HttpAuthenticationMethods.Basic,
    })).Body))
    {
        Console.WriteLine($"token endpoint: expires_in {answer.RootElement.GetProperty("expires_in")}");
    }

    CurlResponse bare = (await Get("v2/", null)).Response;
    Console.WriteLine($"no token: {bare.StatusCode} {bare.Headers.First(h => h.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase)).Value}");
    return 0;
}

int exitCode = await client.CallAsync();
if (exitCode == 0 && mode is "" or "steady")
{
    if (mode == "")
    {
        await server.ResetAsync();
        Console.WriteLine("reset: every token revoked");
    }

    exitCode = await client.CallAsync();
}

// The logins the server judged: wait, at most 10 seconds each, for as many as were made, then list them.
long after = 0;
for (int i = 0; i < client.Logins; i++)
{
    after = (await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.LoginJudged, AfterSequence = after }, TimeSpan.FromSeconds(10))).Sequence;
}

foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"recorded: {login.Account} {login.Mechanism} {(login.IsAccepted ? "accepted" : "refused")}");
}

return exitCode;

async Task<(CurlResponse Response, string Body)> Get(string path, CurlAuthenticationOptions? login)
{
    CurlResponse response = await curl.SendAsync(new CurlRequest(HttpMethod.Get, baseUrl + path) { Tls = trust, Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return (response, await new StreamReader(body).ReadToEndAsync());
}

// The client under test: it keeps its token; with none it logs in for one, and when a call is
// answered 401 it logs in again for a new token and retries the call once.
sealed class TokenClient(CurlClient curl, CurlTlsOptions trust, string baseUrl, string user, string password)
{
    public string? Token { get; set; }

    public int Logins { get; private set; }

    public async Task<int> CallAsync()
    {
        if (Token is null && !await LoginAsync())
        {
            return 1;
        }

        int status = await SendAsync();
        Console.WriteLine($"call: {status}");
        if (status == 401)
        {
            if (!await LoginAsync())
            {
                return 1;
            }

            status = await SendAsync();
            Console.WriteLine($"retry: {status}");
        }

        return status == 200 ? 0 : 1;
    }

    // GET /token with Basic: the JSON answer's token, or false when the login is refused.
    async Task<bool> LoginAsync()
    {
        Logins++;
        CurlResponse response = await curl.SendAsync(new CurlRequest(HttpMethod.Get, baseUrl + "token?service=surl")
        {
            Tls = trust,
            Authentication = new CurlAuthenticationOptions { UserName = user, Password = password, HttpAuthentication = HttpAuthenticationMethods.Basic },
        }, CancellationToken.None);
        await using Stream body = response.Body;
        if (response.StatusCode != 200)
        {
            Console.WriteLine($"login: {response.StatusCode}");
            return false;
        }

        using JsonDocument answer = await JsonDocument.ParseAsync(body);
        string? old = Token;
        Token = answer.RootElement.GetProperty("token").GetString();
        Console.WriteLine(old is null ? "login: token issued" : old == Token ? "login: same token" : "login: new token");
        return true;
    }

    // The API call with the kept token as a Bearer token: the status.
    async Task<int> SendAsync()
    {
        CurlResponse response = await curl.SendAsync(new CurlRequest(HttpMethod.Get, baseUrl + "v2/")
        {
            Tls = trust,
            Authentication = new CurlAuthenticationOptions { XOAuth2Bearer = Token, HttpAuthentication = HttpAuthenticationMethods.Bearer },
        }, CancellationToken.None);
        await using Stream body = response.Body;
        return response.StatusCode;
    }
}
