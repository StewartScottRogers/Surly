// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Lists and makes SFTP folders with Surl.Api and Curl.Api, in one process: a server that lists
// and takes folders, one that lists only, and one that does neither.
// Run: dotnet run program.cs
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

await using CurlClient client = CurlClient.Create();

// 1. Listings and folders on.
await using SurlServer main = await Start(listDirectories: true, allowUploads: true);
await Run("list", main, "/");
await Run("mkdir /reports", main, "/", "mkdir /reports");
await Run("list again", main, "/");
await Run("mkdir /reports", main, "/", "mkdir /reports");
await Run("mkdir /reports/2026", main, "/reports/", "mkdir /reports/2026");
await Run("-mkdir /archive", main, "/", "-mkdir /archive");
await Run("list last", main, "/");

// 2. Listings on, folders off.
await using SurlServer listingOnly = await Start(listDirectories: true, allowUploads: false);
await Run("listing-only list", listingOnly, "/");
await Run("listing-only mkdir", listingOnly, "/", "mkdir /reports");

// 3. Neither.
await using SurlServer closed = await Start(listDirectories: false, allowUploads: false);
await Run("closed list", closed, "/");
return 0;

// A server on a free port with a throwaway host key, one account and one file in memory.
static async Task<SurlServer> Start(bool listDirectories, bool allowUploads)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "sftp://127.0.0.1:0" },
        ThrowawayHostKey = true,
        Accounts = { new SurlAccount("alice", "s3cret") },
        ListDirectories = listDirectories,
        AllowUploads = allowUploads,
    };
    options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
    return await SurlServer.StartAsync(options);
}

// One transfer: the folder at path is listed, after the quote command if there is one. VerifyPeer off is
// curl -k, which skips the check of the throwaway host key.
async Task Run(string label, SurlServer server, string path, string? quote = null)
{
    var request = new CurlRequest(HttpMethod.Get, server.BoundUrl + path.TrimStart('/'))
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" },
        Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } },
    };
    if (quote is not null)
    {
        request = request with { Ftp = new CurlFtpOptions { Quote = [quote] } };
    }

    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string listing = await new StreamReader(body).ReadToEndAsync();
        string names = string.Join(" ", listing.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r').Split(' ')[^1]));
        Console.WriteLine($"{label}: [{names}]");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}");
    }
}
