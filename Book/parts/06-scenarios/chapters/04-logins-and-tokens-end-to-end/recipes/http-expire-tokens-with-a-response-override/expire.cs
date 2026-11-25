// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Tests a client's token refresh at an exact point: a response override counts the API calls made
// since the last token was fetched and answers the next one 401 once the count is spent, as an
// expired token would be. The client must fetch a token again and retry the call once.
// Run: dotnet run expire.cs [-- <case>]
//   (no argument)  the token "expires" after 3 calls; the client makes 6
//   twice          it expires after 2 calls; the client makes 6
//   no-refresh     a client that does not refresh: it stops at the first 401
//   answer         the headers of the override's 401, and what the server judged of the token
//   no-override    no override: 6 calls on one token
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "";
int lifetime = mode == "twice" ? 2 : 3;

// The server: HTTPS with a certificate it makes itself; app's password for the token endpoint,
// and one Bearer token, the account with no user name. token.txt stands in for the endpoint.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("app", "s3cret"), new SurlAccount("", "tok-7f3a") },
    RecordExchanges = true,
};
options.Files["token.txt"] = "tok-7f3a"u8.ToArray();
options.Files["api/orders.json"] = """{"orders":3}"""u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
string baseUrl = $"https://127.0.0.1:{server.BoundUrl.Port}/";

// The override: asked at every request. A fetch of the token starts the count again; an API call
// past the token's lifetime is answered 401, and every other request is left alone (null).
int calls = 0;
if (mode != "no-override")
{
    server.Faults.AddOverride(new SurlFaultScope { Scheme = "https", Stage = FaultStage.Command }, (point, cancel) =>
    {
        if (point.Path == "/token.txt")
        {
            Interlocked.Exchange(ref calls, 0);
            return ValueTask.FromResult<SurlFault?>(null);
        }

        bool expired = point.Path!.StartsWith("/api/") && Interlocked.Increment(ref calls) > lifetime;
        return ValueTask.FromResult(expired ? SurlFault.ProtocolError("401") : null);
    });
}

// The client trusts exactly the certificate the server made, so verification stays on.
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};
await using CurlClient curl = CurlClient.Create();

if (mode == "answer")
{
    // Three calls spend the token; the fourth is the override's 401.
    string token = await FetchToken();
    for (int i = 0; i < 3; i++)
    {
        await Call(token);
    }

    CurlResponse refused = await Send("api/orders.json", Bearer(token));
    await refused.Body.DisposeAsync();
    Console.WriteLine($"call 4: {refused.StatusCode}, challenges: {Challenges(refused)}");
    CurlResponse bare = await curl.SendAsync(new CurlRequest(HttpMethod.Get, baseUrl + "api/orders.json") { Tls = trust }, CancellationToken.None);
    await bare.Body.DisposeAsync();
    Console.WriteLine($"no token at all: {bare.StatusCode}, challenges: {Challenges(bare)}");
    var logins = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>().ToList();
    Console.WriteLine($"logins judged: {logins.Count}; Bearer accepted: {logins.Count(l => l.Mechanism == "Bearer" && l.IsAccepted)} of {logins.Count(l => l.Mechanism == "Bearer")}");
    var faults = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Cast<FaultAppliedEvent>().ToList();
    Console.WriteLine($"fault events: {faults.Count}, {faults[0].Kind} on {faults[0].Target}");
    return 0;
}

// The client under test: it fetches a token once, keeps it, and on a 401 fetches one again and
// retries the call once. The no-refresh client gives up instead.
string kept = await FetchToken();
int fetches = 1;
for (int n = 1; n <= 6; n++)
{
    int status = await Call(kept);
    if (status == 401 && mode != "no-refresh")
    {
        kept = await FetchToken();
        fetches++;
        int retried = await Call(kept);
        Console.WriteLine($"call {n}: 401, token fetched again, retry {retried}");
        continue;
    }

    Console.WriteLine($"call {n}: {status}");
    if (status != 200)
    {
        Console.WriteLine($"FAIL: call {n} was answered {status}");
        return 1;
    }
}

Console.WriteLine($"token fetches: {fetches}");
return 0;

// GET /token.txt with app's password: the token.
async Task<string> FetchToken()
{
    CurlResponse response = await Send("token.txt", new CurlAuthenticationOptions { UserName = "app", Password = "s3cret", HttpAuthentication = HttpAuthenticationMethods.Basic });
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// One API call with the kept token: the status.
async Task<int> Call(string token)
{
    CurlResponse response = await Send("api/orders.json", Bearer(token));
    await response.Body.DisposeAsync();
    return response.StatusCode;
}

// The WWW-Authenticate values of a response, or none.
string Challenges(CurlResponse response)
{
    var values = response.Headers.Where(h => h.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value.Split(' ')[0]).ToList();
    return values.Count == 0 ? "none" : string.Join(", ", values);
}

CurlAuthenticationOptions Bearer(string token) => new() { XOAuth2Bearer = token, HttpAuthentication = HttpAuthenticationMethods.Bearer };

Task<CurlResponse> Send(string path, CurlAuthenticationOptions login) =>
    curl.SendAsync(new CurlRequest(HttpMethod.Get, baseUrl + path) { Tls = trust, Authentication = login }, CancellationToken.None);
