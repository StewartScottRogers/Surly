// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a downloader that fetches an S3 object in byte ranges and, when a connection is cut
// mid-range, asks only for the bytes it is missing. Surl.Api serves the bucket and cuts every
// connection after 1,500,000 bytes; Curl.Api signs each ranged get. server.pem and server.key are in
// the folder.
// Run: dotnet run program.cs [-- <case>]
//   range    bytes 0-1999999, cut and resumed (the default)
//   whole    both halves of the object, joined and compared with it
//   short    a range of 1,000,000 bytes, under the cut
//   refused  a range signed with a wrong secret
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "range";

// big.bin: 4,000,000 random bytes in the bucket media. The server resets every connection once it has
// sent 1,500,000 bytes on it, the response head included.
byte[] big = System.Security.Cryptography.RandomNumberGenerator.GetBytes(4_000_000);
var options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret") },
    Faults = { SurlFault.DropConnection() with { AfterBytes = 1_500_000 } },
    RecordExchanges = true,
};
options.Files["media/big.bin"] = big;
await using SurlServer server = await SurlServer.StartAsync(options);

// The bucket's own host name, sent to the local port; SigV4 signing; the test certificate trusted.
// The timeout bounds every try, the body included.
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(30) });
var network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] };
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
string secret = which == "refused" ? "wrong" : "s3cret";
int cuts = 0;

switch (which)
{
    case "whole":
        var joined = new MemoryStream();
        await GetRange(0, 1_999_999, joined);
        await GetRange(2_000_000, 3_999_999, joined);
        Console.WriteLine($"object whole: {(joined.ToArray().AsSpan().SequenceEqual(big) ? "yes" : "no")}");
        break;
    case "short":
        await GetRange(1_000_000, 1_999_999, new MemoryStream());
        break;
    default:
        var part = new MemoryStream();
        bool done = await GetRange(0, 1_999_999, part);
        if (done)
            Console.WriteLine($"matches the object: {(part.ToArray().AsSpan().SequenceEqual(big.AsSpan(0, 2_000_000)) ? "yes" : "no")}");
        // The server's own account: one fault_applied event, a drop, for each cut connection.
        var drops = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).OfType<FaultAppliedEvent>().ToList();
        Console.WriteLine($"fault events: {drops.Count} for {cuts} cut connections, all Drop: {drops.All(d => d.Kind == FaultKind.Drop)}");
        break;
}

return 0;

// The downloader: bytes first..last into the stream. After a try that ends short it asks only for the
// bytes after those it holds; at most 5 tries. An answer other than 206 is not a cut: it stops there.
async Task<bool> GetRange(long first, long last, MemoryStream into)
{
    long start = into.Length, fetched = 0;
    for (int tries = 0; tries < 5; tries++)
    {
        long from = first + into.Length - start;
        if (from > last) break;
        var request = new CurlRequest(HttpMethod.Get, "https://media.s3.us-east-1.amazonaws.com/big.bin")
        {
            Network = network,
            Tls = trust,
            Authentication = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = secret, AwsSigV4 = "aws:amz:us-east-1:s3" },
            Headers = [new("Range", $"bytes={from}-{last}")],
        };
        long got = 0;
        int status = 0;
        try
        {
            CurlResponse response = await client.SendAsync(request, CancellationToken.None);
            status = response.StatusCode;
            await using Stream body = response.Body;
            if (status != 206)
            {
                Console.WriteLine($"bytes={from}-{last}: {status}, stopped");
                return false;
            }

            byte[] buffer = new byte[65536];
            for (int n; (n = await body.ReadAsync(buffer)) > 0; got += n)
                into.Write(buffer, 0, n);
            Console.WriteLine($"bytes={from}-{last}: {status}, {got} bytes, complete");
        }
        catch (CurlException failure)
        {
            cuts++;
            Console.WriteLine($"bytes={from}-{last}: {status}, {got} bytes, then CurlException {(int)failure.Code} {failure.Code}");
        }

        fetched += got;
    }

    bool whole = first + into.Length - start > last;
    Console.WriteLine(whole ? $"fetched {fetched} bytes for a range of {last - first + 1}" : "gave up");
    return whole;
}
