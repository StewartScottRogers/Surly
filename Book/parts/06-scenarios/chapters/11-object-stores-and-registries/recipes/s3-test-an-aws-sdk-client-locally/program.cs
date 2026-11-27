// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app that writes reports with the AWS SDK for .NET: Surl.Api hosts an S3 bucket in-process,
// the app's AmazonS3Client is pointed at it, and the test reads back what the app wrote.
// Run: dotnet run program.cs [-- <case>]
//   (none)     the app puts a report; the test reads it from the store and lists the bucket (the default)
//   get        the app reads the report back through the SDK, and a key that is not there
//   checksum   how the SDK sent the put, from the server's record of the exchange
//   secret     the client configured with a wrong secret
#:package Surly.Surl.Api@0.1.12
#:package AWSSDK.S3@4.1.2

using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "run";

// The bucket reports, holding last quarter's report, served as plain HTTP for the SDK's custom endpoint.
var options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0" },
    AdditionalArguments = { "--s3-plaintext" },
    Accounts = { new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret") },
    AllowUploads = true,
    ListDirectories = true,
    RecordExchanges = true,
};
options.Files["reports/2026-q2.csv"] = "region,total\nnorth,1100\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

// The app's client, as its settings would make it, with the endpoint pointed at the test server.
var config = new AmazonS3Config
{
    ServiceURL = $"http://127.0.0.1:{server.BoundUrl.Port}",
    ForcePathStyle = true,
    AuthenticationRegion = "us-east-1",
};
var credentials = new BasicAWSCredentials("AKIAIOSFODNN7EXAMPLE", which == "secret" ? "wrong" : "s3cret");
using var s3 = new AmazonS3Client(credentials, config);

try
{
    // What the app does: write this quarter's report.
    PutObjectResponse put = await s3.PutObjectAsync(new PutObjectRequest
    {
        BucketName = "reports", Key = "2026-q3.csv", ContentBody = "region,total\nnorth,1200\n",
    });
    Console.WriteLine($"put: {(int)put.HttpStatusCode}, ETag {put.ETag}");

    if (which == "checksum")
    {
        // How the put was sent, from the server's record of the exchange: the request head's two checksum headers.
        string head = Encoding.ASCII.GetString(server.Exchanges[0].Received).Split("\r\n\r\n")[0];
        foreach (string line in head.Split("\r\n").Where(l => l.StartsWith("X-Amz-Content-SHA256") || l.StartsWith("X-Amz-Trailer")).Order())
            Console.WriteLine($"sent {line}");
        return 0;
    }

    if (which == "get")
    {
        using GetObjectResponse got = await s3.GetObjectAsync("reports", "2026-q3.csv");
        Console.Write($"get: {(int)got.HttpStatusCode}\n{await new StreamReader(got.ResponseStream).ReadToEndAsync()}");
        await s3.GetObjectAsync("reports", "2026-q4.csv");
        return 0;
    }

    // What the test checks: the object in the server's store, and the bucket as the SDK lists it.
    SurlFileContents stored = await server.Files.ReadAsync("/reports/2026-q3.csv");
    Console.Write($"stored:\n{Encoding.UTF8.GetString(stored.Bytes.Span)}");
    ListObjectsV2Response listed = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = "reports" });
    Console.WriteLine($"listed: {string.Join(", ", listed.S3Objects.Select(o => $"{o.Key} {o.Size} bytes"))}");
}
catch (AmazonS3Exception refused)
{
    Console.WriteLine($"AmazonS3Exception {(int)refused.StatusCode} {refused.ErrorCode}: {refused.Message}");
    SurlFolderPage page = await server.Files.ListAsync("/reports");
    Console.WriteLine($"store: {string.Join(", ", page.Entries.Select(e => e.Name))}");
}

return 0;
