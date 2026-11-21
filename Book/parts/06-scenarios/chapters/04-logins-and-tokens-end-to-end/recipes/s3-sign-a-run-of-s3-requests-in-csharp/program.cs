// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an S3 client's signing: put, list and delete with one key pair, then the same steps
// with a wrong secret, against an in-process Surl.Api server. server.pem and server.key are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   (none)           the run with the right secret, then each step with the wrong one (the default)
//   s3-delete        a DELETE on an s3:// URL
//   read-only        the run against a server with uploads off
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "run";

// The server: one key pair, uploads (unless read-only) and listings on, and one report already in the bucket.
var options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret") },
    AllowUploads = which != "read-only",
    ListDirectories = true,
};
options.Files["reports/2026-q2.csv"] = "region,total\nnorth,1100\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create();
var network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] };
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
const string Key = "AKIAIOSFODNN7EXAMPLE";
const string Bucket = "https://reports.s3.us-east-1.amazonaws.com/";

if (which == "s3-delete")
{
    await Step("delete over s3://", HttpMethod.Delete, "s3://reports/2026-q2.csv", "s3cret");
    return 0;
}

// The run: put this quarter's report, list, delete last quarter's, list. First with a wrong secret, then the right one.
foreach (string secret in new[] { "wrong", "s3cret" })
{
    Console.WriteLine($"-- secret {secret}");
    await Step("put", HttpMethod.Put, "s3://reports/2026-q3.csv", secret, "region,total\nnorth,1200\n");
    await Step("list", HttpMethod.Get, "s3://reports/", secret);
    await Step("delete", HttpMethod.Delete, Bucket + "2026-q2.csv", secret);
    await Step("list", HttpMethod.Get, "s3://reports/", secret);

    // What the server holds after the run, read from its store rather than over S3.
    SurlFolderPage page = await server.Files.ListAsync("/reports");
    Console.WriteLine($"store: {string.Join(", ", page.Entries.Select(e => e.Name))}");
}

return 0;

// One signed request. s3:// URLs are signed from the login alone; the https:// DELETE names AwsSigV4.
async Task Step(string label, HttpMethod method, string url, string secret, string? upload = null)
{
    var request = new CurlRequest(method, url)
    {
        Network = network,
        Tls = trust,
        Authentication = new CurlAuthenticationOptions
        {
            UserName = Key,
            Password = secret,
            AwsSigV4 = url.StartsWith("https:") ? "aws:amz:us-east-1:s3" : null,
        },
        Body = upload is null ? null : Encoding.UTF8.GetBytes(upload),
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        // An https:// answer carries an S3 error document; s3:// listings are one key a line.
        Match code = Regex.Match(text, "<Code>(\\w+)</Code>");
        string shown = code.Success ? code.Groups[1].Value : string.Join(" ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Console.WriteLine($"{label}: {response.StatusCode} {shown}".TrimEnd());
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: {failure.Code} {failure.Message}");
    }
}
