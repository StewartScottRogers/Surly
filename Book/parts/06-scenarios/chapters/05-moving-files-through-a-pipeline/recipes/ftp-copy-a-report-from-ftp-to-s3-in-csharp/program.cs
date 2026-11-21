// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a nightly job: download a report over FTP with TLS, put it in an S3 bucket, and compare the
// object's bytes with the source. Both servers run in process; server.pem and server.key are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   (none)      the hop for out/report.csv (the default)
//   missing     the hop for a report the FTP server does not have
//   plain       the hop with the FTP login sent to the plain ftp:// listener
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "copy";

// The FTP server: FTP over TLS from the first byte, plain FTP beside it, one account for the job,
// and tonight's report with Windows line ends.
var ftpOptions = new SurlServerOptions
{
    ListenUrls = { "ftps://127.0.0.1:0", "ftp://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("nightly", "s3cret") },
};
ftpOptions.Files["out/report.csv"] = "region,total\r\nnorth,1200\r\nsouth,950\r\n"u8.ToArray();
await using SurlServer ftp = await SurlServer.StartAsync(ftpOptions);

// The S3 server: one key pair, uploads on, and last night's report in the bucket.
var s3Options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret") },
    AllowUploads = true,
};
s3Options.Files["reports/2026-11-19.csv"] = "region,total\r\nnorth,1180\r\nsouth,940\r\n"u8.ToArray();
await using SurlServer s3 = await SurlServer.StartAsync(s3Options);

await using CurlClient client = CurlClient.Create();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };

// The job, as the test runs it: any failure stops it before the upload.
string source = which == "missing" ? "out/missing.csv" : "out/report.csv";
try
{
    // Download over FTP: ftps:// is TLS before the login is sent; the plain listener is the second URL.
    Uri listener = which == "plain" ? ftp.BoundUrls[1] : ftp.BoundUrl;
    CurlResponse got = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{listener}{source}")
    {
        Tls = trust,
        Authentication = new CurlAuthenticationOptions { UserName = "nightly", Password = "s3cret" },
    }, CancellationToken.None);
    var report = new MemoryStream();
    await using (Stream body = got.Body)
    {
        await body.CopyToAsync(report);
    }

    Console.WriteLine($"ftp: {report.Length} bytes");

    // Upload to S3: a PUT with a body on an s3:// URL, signed with the key pair.
    CurlResponse put = await client.SendAsync(new CurlRequest(HttpMethod.Put, "s3://reports/2026-11-20.csv")
    {
        Network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{s3.BoundUrl.Port}"] },
        Tls = trust,
        Authentication = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = "s3cret" },
        Body = report.ToArray(),
    }, CancellationToken.None);
    await put.Body.DisposeAsync();
    Console.WriteLine($"s3: {put.StatusCode}");
}
catch (CurlException failure)
{
    Console.WriteLine($"job stopped: {failure.Code} ({failure.Message})");
}

// The test's check: the object's bytes against the source's, both read from the servers' stores.
byte[] expected = (await ftp.Files.ReadAsync("/out/report.csv")).Bytes.ToArray();
try
{
    byte[] stored = (await s3.Files.ReadAsync("/reports/2026-11-20.csv")).Bytes.ToArray();
    Console.WriteLine($"object: {stored.Length} bytes, {(stored.AsSpan().SequenceEqual(expected) ? "identical to the source" : "DIFFERENT from the source")}");
    return stored.AsSpan().SequenceEqual(expected) ? 0 : 1;
}
catch (SurlNotFoundException)
{
    Console.WriteLine("object: none");
    return 1;
}
