// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Asks for HTTP/3 with Curl.Api from a Surl.Api server that has no HTTP/3, and shows the request
// that may fall back to TCP succeed and the one that insists on HTTP/3 fail.
// Run: dotnet run fallback.cs [-- only | swallowed | swallowed-only | h3]
//   (no argument)  HttpVersion.Http3 against nothing on UDP
//   only           HttpVersion.Http3Only against nothing on UDP
//   swallowed      HttpVersion.Http3 against a TFTP listener on the same port number
//   swallowed-only HttpVersion.Http3Only against it, with a 2 s connect timeout
//   h3             HttpVersion.Http3 against a server with --http3
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Http;
using Curl.Api.Options.Tls;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "";

// The HTTPS server: the certificate and key from setup; --http3 only for h3.
var options = new SurlServerOptions { ListenUrls = { "https://127.0.0.1:0" }, CertificateFile = "server.pem", KeyFile = "server.key" };
if (mode == "h3")
{
    options.AdditionalArguments.Add("--http3");
}

options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
SurlServer server = await SurlServer.StartAsync(options);

// With swallowed, a TFTP server takes the same port number on UDP: QUIC packets then reach a
// listener that does not speak QUIC. A UDP port can be taken when its TCP twin is free, so on
// a refusal both servers start again on another port.
SurlServer? tftp = null;
for (int attempt = 1; mode.StartsWith("swallowed") && tftp is null; attempt++)
{
    var tftpOptions = new SurlServerOptions { ListenUrls = { $"tftp://127.0.0.1:{server.BoundUrl.Port}" }, RecordExchanges = true };
    tftpOptions.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
    try
    {
        tftp = await SurlServer.StartAsync(tftpOptions);
    }
    catch (SurlServerStartException) when (attempt < 5)
    {
        await server.DisposeAsync();
        server = await SurlServer.StartAsync(options);
    }
}

// The first Curl.Api request in a process pays a one-time set-up, and so does the first over QUIC;
// pay them on a client of its own, so the request that counts neither waits nor finds a connection.
ReadOnlyMemory<byte>? ca = File.ReadAllBytes("server.pem");
var tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = ca } };
await using (CurlClient warmUp = CurlClient.Create(new CurlClientOptions { Tls = tls }))
{
    foreach (HttpVersion version in mode == "h3" ? new[] { HttpVersion.None, HttpVersion.Http3Only } : new[] { HttpVersion.None })
    {
        var warm = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt") { Http = new CurlHttpOptions { HttpVersion = version } };
        CurlResponse first = await warmUp.SendAsync(warm, CancellationToken.None);
        await first.Body.DisposeAsync();
    }
}

// The client trusts server.pem.
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = tls });

// Http3 may fall back to TCP; Http3Only may not. HappyEyeballsTimeout gives QUIC 4 s before TCP
// joins the race (h3); ConnectTimeout bounds a QUIC handshake that never finishes (swallowed-only).
var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
{
    Http = new CurlHttpOptions { HttpVersion = mode.EndsWith("only") ? HttpVersion.Http3Only : HttpVersion.Http3 },
    Network = mode switch
    {
        "h3" => new CurlNetworkOptions { HappyEyeballsTimeout = TimeSpan.FromSeconds(4) },
        "swallowed-only" => new CurlNetworkOptions { ConnectTimeout = TimeSpan.FromSeconds(2) },
        _ => null,
    },
};
int exit = 0;
try
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine($"HTTP/{response.Version} {response.StatusCode}");
    Console.Write(await new StreamReader(body).ReadToEndAsync());
}
catch (CurlException e)
{
    // The code is curl's exit code; the message's wording is the platform's.
    Console.WriteLine($"CurlException: {e.Code} ({(int)e.Code})");
    exit = (int)e.Code;
}

if (tftp is not null)
{
    Console.WriteLine($"QUIC packets the TFTP listener took as exchanges: {(tftp.Exchanges.Count > 0 ? "some" : "none")}");
    await tftp.DisposeAsync();
}

await server.DisposeAsync();

return exit;
