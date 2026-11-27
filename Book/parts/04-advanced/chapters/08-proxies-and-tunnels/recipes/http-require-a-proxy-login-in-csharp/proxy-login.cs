// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a client that logs in to its proxy: Surl.Api runs an origin with its own account and
// two proxies with a proxy account, and Curl.Api sends each login to its own hop.
// Run: dotnet run proxy-login.cs -- <case>
//   fetch         Digest to the proxy and Digest to the origin (the default)
//   no-login      no proxy login: the 407 and what it offers
//   wrong         a wrong proxy password
//   basic-http    Basic to the http proxy
//   https-offers  no proxy login to the https proxy: what its 407 offers
//   basic-https   Basic to the https proxy
//   plaintext     Basic to an http proxy that allows passwords in clear
//   swapped       the origin's account at the proxy, then the proxy's account at the origin
//   events        the logins the server judged during a fetch
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "fetch";

// One server: the origin with account web, an http and an https proxy sharing proxy account app.
// SurlServerOptions has no proxy properties, so --proxy and -U go in as surl's own words.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    AdditionalArguments = { "--proxy", "http://127.0.0.1:0", "--proxy", "https://127.0.0.1:0", "-U", "app:proxy-pw" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    AllowPlaintextAuthentication = which == "plaintext",
    RecordExchanges = true,
};
options.Accounts.Add(new SurlAccount("web", "site-pw"));
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri origin = server.BoundUrls[0], httpProxy = server.BoundUrls[1], httpsProxy = server.BoundUrls[2];

// One request for the origin's file through a proxy, with a proxy login and an origin login. The
// answer is read before the client goes: the status, the body of a 200, and what a 407 offers.
async Task<(int Status, string Text, string[] Offers)> Fetch(Uri proxy, string? proxyLogin, HttpAuthenticationMethods proxyMethod, string originLogin)
{
    await using CurlClient client = CurlClient.Create(new CurlClientOptions
    {
        Proxy = new CurlProxyOptions { Proxy = proxy.ToString() },
        Tls = new CurlTlsOptions { Proxy = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" } },
        Authentication = new CurlAuthenticationOptions
        {
            ProxyUserPassword = proxyLogin, ProxyAuthentication = proxyMethod,
            UserPassword = originLogin, HttpAuthentication = HttpAuthenticationMethods.Digest,
        },
    });
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{origin}hello.txt"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    string[] offers = response.Headers.Where(h => h.Key == "Proxy-Authenticate").Select(h => Offer(h.Value)).ToArray();
    return (response.StatusCode, text, offers);
}

// "Digest realm=..., algorithm=SHA-256, ..." becomes "Digest SHA-256"; "Basic realm=..." becomes "Basic".
static string Offer(string value)
{
    int at = value.IndexOf("algorithm=");
    return value.Split(' ')[0] + (at < 0 ? "" : " " + value[(at + 10)..].Split(',')[0]);
}

void Show(string label, (int Status, string Text, string[] Offers) answer)
{
    Console.WriteLine($"{label}: {answer.Status}{(answer.Status == 200 ? ", " + answer.Text.TrimEnd() : "")}");
    foreach (string offer in answer.Offers) Console.WriteLine($"offers: {offer}");
}

var digest = HttpAuthenticationMethods.Digest;
var basic = HttpAuthenticationMethods.Basic;
switch (which)
{
    case "fetch": Show("fetch", await Fetch(httpProxy, "app:proxy-pw", digest, "web:site-pw")); break;
    case "no-login": Show("no proxy login", await Fetch(httpProxy, null, digest, "web:site-pw")); break;
    case "wrong": Show("wrong proxy password", await Fetch(httpProxy, "app:guess", digest, "web:site-pw")); break;
    case "basic-http": Show("Basic to the http proxy", await Fetch(httpProxy, "app:proxy-pw", basic, "web:site-pw")); break;
    case "https-offers": Show("no proxy login", await Fetch(httpsProxy, null, digest, "web:site-pw")); break;
    case "basic-https": Show("Basic to the https proxy", await Fetch(httpsProxy, "app:proxy-pw", basic, "web:site-pw")); break;
    case "plaintext": Show("Basic to the http proxy", await Fetch(httpProxy, "app:proxy-pw", basic, "web:site-pw")); break;
    case "swapped":
        Show("origin account at the proxy", await Fetch(httpProxy, "web:site-pw", digest, "web:site-pw"));
        Show("proxy account at the origin", await Fetch(httpProxy, "app:proxy-pw", digest, "app:proxy-pw"));
        break;
    case "events":
        Show("fetch", await Fetch(httpProxy, "app:proxy-pw", digest, "web:site-pw"));
        foreach (var e in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
            Console.WriteLine($"{e.Scheme} {e.Mechanism} {e.Account}: {(e.IsAccepted ? "accepted" : "refused")}");
        break;
}
return 0;
