// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a client that reaches an HTTPS origin through a proxy: Surl.Api runs a TLS origin and
// a proxy in one server that records and logs what it sees, and Curl.Api fetches through a CONNECT tunnel.
// Run: dotnet run tunnel.cs -- <case>
//   fetch        fetch hello.txt through the http proxy (the default)
//   seen         what the proxy recorded and logged of that fetch
//   untrusted    the same fetch without trusting the origin's certificate
//   https-proxy  through the https proxy, trusting both certificates
//   proxy-only   through the https proxy, trusting only the origin's certificate
//   refused      through a proxy whose fault rule answers CONNECT with 503
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "fetch";

// One server: the TLS origin on the listen URL, with the certificate and key openssl wrote, and an
// http and an https proxy listener beside it. It records every exchange and logs at verbose.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    AdditionalArguments = { "--proxy", "http://127.0.0.1:0", "--proxy", "https://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
if (which == "refused")
{
    options.AdditionalArguments.Add("--fault");
    options.AdditionalArguments.Add("error;scheme=http-proxy;command=CONNECT;reply=503");
}
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri origin = server.BoundUrls[0], httpProxy = server.BoundUrls[1], httpsProxy = server.BoundUrls[2];

// The client under test: the proxy for every request, and separate TLS trust for origin and proxy.
bool viaHttps = which is "https-proxy" or "proxy-only";
await using CurlClient client = CurlClient.Create(new CurlClientOptions
{
    Proxy = new CurlProxyOptions { Proxy = (viaHttps ? httpsProxy : httpProxy).ToString() },
    Tls = new CurlTlsOptions
    {
        Origin = which == "untrusted" ? new CurlTlsPeerOptions() : new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" },
        Proxy = which == "https-proxy" ? new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" } : new CurlTlsPeerOptions(),
    },
});
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{origin}hello.txt"), CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine($"{response.StatusCode}, {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
}
catch (CurlException failure)
{
    Console.WriteLine($"CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
    return 0;
}

if (which == "seen")
{
    // The record can trail the answer by a moment: look again until the proxy and the origin each
    // hold a whole request head, for up to 10 s.
    bool Recorded(Uri listener) => server.Exchanges.Any(e => e.ListenUrl == listener && Encoding.Latin1.GetString(e.Received).Contains("\r\n\r\n"));
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); !(Recorded(httpProxy) && Recorded(origin)) && DateTime.UtcNow < deadline;)
    {
        await Task.Delay(100);
    }

    // What crossed the proxy: the CONNECT head in clear, then only TLS records.
    SurlExchangeRecord exchange = server.Exchanges.Single(e => e.ListenUrl == httpProxy);
    string received = Encoding.Latin1.GetString(exchange.Received);
    Console.WriteLine($"the proxy received: {received.Split("\r\n")[0]}");
    Console.WriteLine($"GET /hello.txt in the proxy's bytes: {(received.Contains("GET /hello.txt") ? "yes" : "no")}");
    SurlExchangeRecord atOrigin = server.Exchanges.Single(e => e.ListenUrl == origin);
    Console.WriteLine($"the origin received over {atOrigin.Scheme}: {Encoding.Latin1.GetString(atOrigin.Received).Split("\r\n")[0]}");
    // The proxy's lines of the log: those of its exchange number, #N.
    string[] lines = log.ToString().Split('\n').Where(l => l.StartsWith($"#{exchange.ExchangeId} ")).ToArray();
    Console.WriteLine(lines.First(l => l.Contains("< CONNECT ")).TrimEnd());
    Console.WriteLine(lines.First(l => l.Contains("Tunnelling to ")).TrimEnd());
}
return 0;
