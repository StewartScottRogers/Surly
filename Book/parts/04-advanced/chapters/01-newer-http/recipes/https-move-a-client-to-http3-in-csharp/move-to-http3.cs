// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A CurlClient with an alt-svc file learns from a Surl.Api server's Alt-Svc header that it
// serves HTTP/3, and its next request uses it; the server's record shows which went over QUIC.
// Run: dotnet run move-to-http3.cs [-- none | http1.1]
//   (no argument) the second request asks for HttpVersion.Http3
//   none          the second request has no version option (HttpVersion.None)
//   http1.1       the second request asks for HttpVersion.Http11
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Options.Http;
using Curl.Api.Options.Tls;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "";

// The server: HTTPS with the certificate and key from setup, and --http3, which has no property.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" }, CertificateFile = "server.pem", KeyFile = "server.key", RecordExchanges = true,
};
options.AdditionalArguments.Add("--http3");
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The first Curl.Api request in a process pays a one-time set-up, and so does the first over QUIC;
// pay both on a client of its own, so the requests that count neither wait nor find a connection.
ReadOnlyMemory<byte>? ca = File.ReadAllBytes("server.pem");
var tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = ca } };
await using (CurlClient warmUp = CurlClient.Create(new CurlClientOptions { Tls = tls }))
{
    foreach (HttpVersion version in new[] { HttpVersion.None, HttpVersion.Http3Only })
    {
        var warm = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt") { Http = new CurlHttpOptions { HttpVersion = version } };
        CurlResponse first = await warmUp.SendAsync(warm, CancellationToken.None);
        await first.Body.DisposeAsync();
    }
}

// The client trusts server.pem and keeps its alt-svc cache in alt-svc.txt, open to every version.
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = tls });
File.Delete("alt-svc.txt");
client.AltSvc.Use(Path.GetFullPath("alt-svc.txt"), AltSvcControl.H1 | AltSvcControl.H2 | AltSvcControl.H3);

// First request: nothing known yet, so TCP; the answer's Alt-Svc lands in the file.
await Fetch("first", HttpVersion.None);
foreach (CurlAltSvcEntry entry in await client.AltSvc.GetEntriesAsync(CancellationToken.None))
{
    string samePort = entry.DestinationPort == entry.SourcePort ? "the same port" : "another port";
    Console.WriteLine($"saved: {entry.SourceAlpn} {entry.SourceHost} to {entry.DestinationAlpn} {entry.DestinationHost} on {samePort}");
}

// Second request: the entry is read before it connects. HappyEyeballsTimeout holds TCP back 4 s
// while QUIC finishes its handshake.
await Fetch("second", mode switch { "none" => HttpVersion.None, "http1.1" => HttpVersion.Http11, _ => HttpVersion.Http3 });
return 0;

async Task Fetch(string which, HttpVersion version)
{
    int before = server.Exchanges.Count;
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
    {
        Http = new CurlHttpOptions { HttpVersion = version },
        Network = new CurlNetworkOptions { HappyEyeballsTimeout = TimeSpan.FromSeconds(4) },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    string altSvc = string.Join("; ", response.Headers.Where(h => h.Key.Equals("alt-svc", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value));
    Console.WriteLine($"{which}: HTTP/{response.Version} {response.StatusCode}, Alt-Svc [{altSvc}]");

    // A QUIC exchange records its bytes per QUIC stream; a TCP one has no stream IDs. The record can
    // trail the answer by a moment, so after an HTTP/3 answer look again until it is there, for up to 10 s.
    bool Quic() => server.Exchanges.Skip(before).Any(e => e.Chunks.Any(c => c.StreamId is not null));
    for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); response.Version.Major == 3 && !Quic() && DateTime.UtcNow < deadline;)
    {
        await Task.Delay(100);
    }

    bool quic = Quic();
    Console.WriteLine($"{which}: the server saw {(quic ? "QUIC" : "TCP")}");
}
