// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a client whose proxy refuses to open a tunnel: Surl.Api runs an origin and a proxy
// whose fault rule answers every CONNECT with 503, and Curl.Api fetches through it.
// Run: dotnet run tunnel-refused.cs -- <case>
//   refused   an https:// URL through the proxy, which needs a tunnel (the default)
//   forward   an http:// URL through the same proxy, forwarded without a tunnel
//   busy      an http:// URL the origin itself answers with 503
//   forced    the http:// URL again, with HttpProxyTunnel asking for a tunnel
//   seen      what the server logged and recorded of a refused tunnel
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "refused";

// One server: the origin over http and https with the certificate openssl wrote, and a proxy
// listener beside it. Two fault rules: CONNECT on the proxy is answered 503, and so is the origin's
// /busy.txt. The server records every exchange and logs at verbose.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "https://127.0.0.1:0" },
    AdditionalArguments = { "--proxy", "http://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Faults =
    {
        SurlFault.ProtocolError("503") with { Scheme = "http-proxy", Command = "CONNECT" },
        SurlFault.ProtocolError("503") with { Scheme = "http", Path = "/busy.txt" },
    },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri plain = server.BoundUrls[0], secure = server.BoundUrls[1], proxy = server.BoundUrls[2];

// The client under test: every request through the proxy, the origin's certificate trusted.
await using CurlClient client = CurlClient.Create(new CurlClientOptions
{
    Proxy = new CurlProxyOptions { Proxy = proxy.ToString(), HttpProxyTunnel = which == "forced" },
    Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" } },
    Timeout = TimeSpan.FromSeconds(20),
});
string url = which switch
{
    "refused" or "seen" => $"{secure}hello.txt",
    "busy" => $"{plain}busy.txt",
    _ => $"{plain}hello.txt",
};
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine($"{response.StatusCode}, {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
}
catch (CurlException failure)
{
    Console.WriteLine($"CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
}

if (which == "seen")
{
    await server.StopAsync();
    // The proxy's line naming the rule, and how many exchanges reached the https origin.
    Console.WriteLine(log.ToString().Split('\n').First(l => l.Contains("by fault rule")).TrimEnd());
    Console.WriteLine($"exchanges at the https origin: {server.Exchanges.Count(e => e.ListenUrl == secure)}");
}
return 0;
