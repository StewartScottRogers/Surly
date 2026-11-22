// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Lists a blob container with Curl.Api against three Surl.Api servers, in one process: one that allows
// listings, one that does not, and one that also allows anonymous callers. Run: dotnet run program.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// Three servers on one set of blobs, each in memory: the container photos holds four blobs.
// server.pem and server.key, a certificate for 127.0.0.1, are in the folder.
Task<SurlServer> Start(bool listings, bool anonymous)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "az://127.0.0.1:0" },
        CertificateFile = "server.pem",
        KeyFile = "server.key",
        ListDirectories = listings,
        AllowAnonymous = anonymous,
    };
    options.Accounts.Add(new SurlAccount("devstoreaccount1", "c2VjcmV0"));
    options.Accounts.Add(new SurlAccount("", "t0ken-abc"));
    options.Files["photos/readme.txt"] = "read me\n"u8.ToArray();
    options.Files["photos/2025/dec.jpg"] = "december\n"u8.ToArray();
    options.Files["photos/2026/jan.jpg"] = "january\n"u8.ToArray();
    options.Files["photos/2026/feb.jpg"] = "february\n"u8.ToArray();
    return SurlServer.StartAsync(options);
}

await using SurlServer main = await Start(listings: true, anonymous: false);
await using SurlServer noListing = await Start(listings: false, anonymous: false);
await using SurlServer open = await Start(listings: true, anonymous: true);

// The client trusts the certificate. The URL is the emulator's path style: account, container, then the
// blob name; a path that ends in a slash is a listing.
await using CurlClient client = CurlClient.Create();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
var bearer = new CurlAuthenticationOptions { XOAuth2Bearer = "t0ken-abc" };
var sharedKey = new CurlAuthenticationOptions { UserName = "devstoreaccount1", Password = "c2VjcmV0" };

await Show("prefix", main, "devstoreaccount1/photos/2026/", bearer);
await Show("top", main, "devstoreaccount1/photos/", bearer);
await Show("empty", main, "devstoreaccount1/photos/2030/", bearer);
await Show("no container", main, "devstoreaccount1/videos/", bearer);
await Show("shared key", main, "devstoreaccount1/photos/2026/", sharedKey);
await Show("no token", main, "devstoreaccount1/photos/2026/", new CurlAuthenticationOptions());
await Show("listing off", noListing, "devstoreaccount1/photos/", bearer);
await Show("listing off, get", noListing, "devstoreaccount1/photos/readme.txt", bearer);
await Show("anonymous", open, "devstoreaccount1/photos/2026/", new CurlAuthenticationOptions());
return 0;

// One request. A listing's body is the blob names, one to a line. A refusal throws CurlException; the
// program prints its code and the first line of its message.
async Task Show(string label, SurlServer server, string path, CurlAuthenticationOptions authentication)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}") { Tls = trust, Authentication = authentication };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        Console.WriteLine($"{label}: {response.StatusCode} [{string.Join(", ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries))}]");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}: {failure.Message.Split('\n')[0]}");
    }
}
