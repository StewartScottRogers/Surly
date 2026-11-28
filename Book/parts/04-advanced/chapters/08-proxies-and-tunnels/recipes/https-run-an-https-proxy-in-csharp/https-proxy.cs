// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a client that reaches its proxy over TLS: Surl.Api runs an https proxy with its own
// certificate and an https origin with another, and Curl.Api fetches through the proxy with separate
// trust settings for each.
// Run: dotnet run https-proxy.cs -- <case>
//   fetch           an http:// origin through the https proxy, trusting the proxy (the default)
//   untrusted       the same, trusting nothing
//   origin-trust    the same, with the proxy's certificate given as the origin's authority
//   seen            the proxy's log line for the forwarded request
//   both            an https:// origin through the proxy, trusting each with its own certificate
//   swapped         the same, with the two certificates swapped
//   origin-unchecked  the origin's check skipped, the proxy trusted by nobody
//   proxy-unchecked   the proxy's check skipped, the origin trusted
//   no-certificate  starting an https proxy with no certificate
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "fetch";

if (which == "no-certificate")
{
    try
    {
        await using SurlServer refused = await SurlServer.StartAsync(new SurlServerOptions
        {
            ListenUrls = { "http://127.0.0.1:0" },
            AdditionalArguments = { "--proxy", "https://127.0.0.1:0" },
        });
        Console.WriteLine("started");
    }
    catch (SurlServerStartException e)
    {
        Console.WriteLine($"SurlServerStartException {e.ExitCode}: {e.Message}");
    }
    return 0;
}

// The proxy's server: an https proxy listener with the proxy's certificate, logging at verbose, and a
// plain http origin beside it (Surl.Api needs one listen URL that is not a proxy).
var log = new StringWriter();
var proxyOptions = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    AdditionalArguments = { "--proxy", "https://127.0.0.1:0" },
    CertificateFile = "proxy.pem",
    KeyFile = "proxy.key",
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
proxyOptions.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer proxyServer = await SurlServer.StartAsync(proxyOptions);
Uri plainOrigin = proxyServer.BoundUrls[0], proxy = proxyServer.BoundUrls[1];

// The https origin, with a certificate of its own.
var originOptions = new SurlServerOptions { ListenUrls = { "https://127.0.0.1:0" }, CertificateFile = "origin.pem", KeyFile = "origin.key" };
originOptions.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer originServer = await SurlServer.StartAsync(originOptions);
Uri secureOrigin = originServer.BoundUrl;

// The client: CurlTlsOptions.Proxy checks the proxy, CurlTlsOptions.Origin the origin.
var trusts = new CurlTlsPeerOptions { CertificateAuthorityFile = "proxy.pem" };
var trustsOrigin = new CurlTlsPeerOptions { CertificateAuthorityFile = "origin.pem" };
var skipCheck = new CurlTlsPeerOptions { VerifyPeer = false, VerifyHost = false };
var none = new CurlTlsPeerOptions();
(CurlTlsPeerOptions toProxy, CurlTlsPeerOptions toOrigin, Uri origin) = which switch
{
    "untrusted" => (none, none, plainOrigin),
    "origin-trust" => (none, trusts, plainOrigin),
    "both" => (trusts, trustsOrigin, secureOrigin),
    "swapped" => (trustsOrigin, trusts, secureOrigin),
    "origin-unchecked" => (none, skipCheck, secureOrigin),
    "proxy-unchecked" => (skipCheck, trustsOrigin, secureOrigin),
    _ => (trusts, none, plainOrigin),
};
await using CurlClient client = CurlClient.Create(new CurlClientOptions
{
    Proxy = new CurlProxyOptions { Proxy = proxy.ToString() },
    Tls = new CurlTlsOptions { Proxy = toProxy, Origin = toOrigin },
    Timeout = TimeSpan.FromSeconds(20),
});
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{origin}hello.txt"), CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine($"{response.StatusCode}, {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
}
catch (CurlException failure)
{
    Console.WriteLine($"CurlException {(int)failure.Code} {failure.Code}");
}

if (which == "seen")
{
    await proxyServer.StopAsync();
    Console.WriteLine(log.ToString().Split('\n').First(l => l.Contains("Forwarded GET")).TrimEnd());
}
return 0;
