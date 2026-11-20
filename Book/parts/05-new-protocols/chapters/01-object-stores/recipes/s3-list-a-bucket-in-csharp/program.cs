// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Lists a bucket with Curl.Api against three Surl.Api servers, in one process: one that allows listings,
// one that does not, and one that also allows anonymous callers. Run: dotnet run program.cs
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// Three servers on one set of files, each in memory: the bucket photos holds three objects.
// server.pem and server.key, a certificate for 127.0.0.1 and for every bucket's host name, are in the folder.
Task<SurlServer> Start(bool listings, bool anonymous)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "s3://127.0.0.1:0" },
        CertificateFile = "server.pem",
        KeyFile = "server.key",
        ListDirectories = listings,
        AllowAnonymous = anonymous,
    };
    options.Accounts.Add(new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret"));
    options.Files["photos/readme.txt"] = "read me\n"u8.ToArray();
    options.Files["photos/2025/dec.jpg"] = "december\n"u8.ToArray();
    options.Files["photos/2026/jan.jpg"] = "january\n"u8.ToArray();
    options.Files["photos/2026/feb.jpg"] = "february\n"u8.ToArray();
    return SurlServer.StartAsync(options);
}

await using SurlServer main = await Start(listings: true, anonymous: false);
await using SurlServer noListing = await Start(listings: false, anonymous: false);
await using SurlServer open = await Start(listings: true, anonymous: true);

// The client trusts the certificate. An s3:// URL names the bucket as its host and curl sends it to
// <bucket>.s3.us-east-1.amazonaws.com; ConnectTo (--connect-to) sends the connection to the server instead.
await using CurlClient client = CurlClient.Create();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };

await Show("prefix", main, "s3://photos/2026/", signed: true);
await Show("top", main, "s3://photos/", signed: true);
await Show("empty", main, "s3://photos/2030/", signed: true);
await Show("no bucket", main, "s3://videos/", signed: true);
await Show("unsigned", main, "s3://photos/2026/", signed: false);
await Show("listing off", noListing, "s3://photos/", signed: true);
await Show("listing off, get", noListing, "s3://photos/readme.txt", signed: true);
await Show("anonymous", open, "s3://photos/2026/", signed: false);
return 0;

// One request. A listing is a URL that ends in a slash, and its body is the keys, one to a line. A refusal
// throws CurlException; the program prints its code and message.
async Task Show(string label, SurlServer server, string url, bool signed)
{
    var request = new CurlRequest(HttpMethod.Get, url)
    {
        Network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] },
        Tls = trust,
        Authentication = signed ? new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = "s3cret" } : new CurlAuthenticationOptions(),
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        Console.WriteLine($"{label}: {response.StatusCode} [{string.Join(", ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries))}]");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
    }
}
