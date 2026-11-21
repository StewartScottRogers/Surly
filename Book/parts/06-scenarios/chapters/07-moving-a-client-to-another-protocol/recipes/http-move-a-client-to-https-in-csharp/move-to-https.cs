// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A client's move from HTTP to HTTPS, tested in process: one server with both listeners, a client
// allowed only HTTPS, the server's record of what came in, then the server again with HTTPS alone.
// Run: dotnet run move-to-https.cs [-- <step>]
//   moved           during the move: the moved client fetches over https (the default)
//   unmoved         during the move: a client not yet moved fetches over http
//   guard           during the move: a plain URL left in the moved client
//   untrusted       during the move: an https client that does not trust the test certificate
//   record          during the move: all three calls, then the scheme of every exchange recorded
//   after           after the move: the server again on its https address alone; the moved client
//   after-plain     after the move: a client still on the plain URL
//   after-record    after the move: both clients, then the scheme of every exchange recorded
//   no-certificate  an https listen URL with no certificate
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string step = args.Length > 0 ? args[0] : "moved";

if (step == "no-certificate")
{
    try
    {
        await StartAsync(certificate: false, "https://127.0.0.1:0");
    }
    catch (SurlServerStartException refused)
    {
        Console.WriteLine($"SurlServerStartException {(int)refused.ExitCode} {refused.ExitCode}");
        return 0;
    }
}

// During the move: the same file over plain HTTP and HTTPS, every exchange recorded.
SurlServer server = await StartAsync(certificate: true, "http://127.0.0.1:0", "https://127.0.0.1:0");
string plain = $"{server.BoundUrls[0]}hello.txt", secure = $"{server.BoundUrls[1]}hello.txt";

// The moved client: HTTPS only (curl --proto =https), trusting the test certificate (--cacert).
await using CurlClient moved = CurlClient.Create(new CurlClientOptions
{
    Network = new CurlNetworkOptions { Protocols = "https" },
    Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" } },
});
// A client not yet moved: any scheme, no trust settings.
await using CurlClient unmoved = CurlClient.Create();

if (step.StartsWith("after"))
{
    // After the move: the same server started again with its HTTPS listen URL alone, port included.
    await server.StopAsync();
    server = await StartAsync(certificate: true, server.BoundUrls[1].ToString());
    Console.WriteLine($"listening: {string.Join(" ", server.BoundUrls.Select(u => u.Scheme))}");
}

switch (step)
{
    case "moved" or "after":
        await Fetch("moved client, https", moved, secure);
        break;
    case "unmoved":
        await Fetch("unmoved client, http", unmoved, plain);
        break;
    case "guard":
        await Fetch("moved client, http", moved, plain);
        break;
    case "untrusted":
        await using (CurlClient untrusted = CurlClient.Create(new CurlClientOptions { Network = new CurlNetworkOptions { Protocols = "https" } }))
        {
            await Fetch("client without the CA certificate, https", untrusted, secure);
        }

        break;
    case "after-plain":
        await Fetch("unmoved client, http", unmoved, plain);
        break;
    case "record":
        await Fetch("moved client, https", moved, secure);
        await Fetch("unmoved client, http", unmoved, plain);
        await Fetch("moved client, http", moved, plain);
        Console.WriteLine($"exchanges: {string.Join(" ", server.Exchanges.Select(e => e.Scheme))}");
        break;
    case "after-record":
        await Fetch("moved client, https", moved, secure);
        await Fetch("unmoved client, http", unmoved, plain);
        Console.WriteLine($"exchanges: {string.Join(" ", server.Exchanges.Select(e => e.Scheme))}");
        break;
}

await server.StopAsync();
return 0;

static async Task<SurlServer> StartAsync(bool certificate, params string[] listenUrls)
{
    var options = new SurlServerOptions { RecordExchanges = true };
    if (certificate)
    {
        options.CertificateFile = "server.pem";
        options.KeyFile = "server.key";
    }

    foreach (string url in listenUrls) options.ListenUrls.Add(url);
    options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
    return await SurlServer.StartAsync(options);
}

static async Task Fetch(string who, CurlClient client, string url)
{
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
        await using Stream body = response.Body;
        Console.WriteLine($"{who}: {response.StatusCode} {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{who}: CurlException {(int)failure.Code} {failure.Code}");
    }
}
