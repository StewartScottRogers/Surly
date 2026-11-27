// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of an app's multipart upload to S3: Surl.Api hosts a bucket that takes uploads of at most
// 12 MiB, and Curl.Api starts an upload, puts the parts and completes it. server.pem and server.key are
// in the folder.
// Run: dotnet run program.cs [-- <case>]
//   upload   11,000,000 bytes in 5 MiB parts, then read back from the store (the default)
//   huge     13,000,000 bytes in 5 MiB parts, over the limit
//   single   13,000,000 bytes in one put
//   small    11,000,000 bytes in 1,000,000-byte parts
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "upload";

// The bucket backups, empty, taking signed uploads of at most 12 MiB (12,582,912 bytes).
var options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret") },
    AllowUploads = true,
    MaxFileSize = 12 * 1024 * 1024,
};
options.Files["backups/.keep"] = [];
await using SurlServer server = await SurlServer.StartAsync(options);

// Each request goes to the bucket's own host name at the local port, signed with SigV4.
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(60) });
var network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] };
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
var signing = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = "s3cret", AwsSigV4 = "aws:amz:us-east-1:s3" };
const string Url = "https://backups.s3.us-east-1.amazonaws.com/";

byte[] file = System.Security.Cryptography.RandomNumberGenerator.GetBytes(which is "huge" or "single" ? 13_000_000 : 11_000_000);

if (which == "single")
{
    (int status, string body, _) = await Send(HttpMethod.Put, "huge.tar", file);
    Console.WriteLine($"put: {status} {Code(body)}");
    return 0;
}

string key = which == "huge" ? "huge.tar" : "backup.tar";
bool stored = await UploadInParts(key, file, which == "small" ? 1_000_000 : 5 * 1024 * 1024);
if (stored)
{
    // Read back from the server's store, not over S3.
    SurlFileContents kept = await server.Files.ReadAsync($"/backups/{key}");
    Console.WriteLine($"stored: {kept.Bytes.Length} bytes, same as the file: {kept.Bytes.Span.SequenceEqual(file)}");
}
else
{
    SurlFolderPage page = await server.Files.ListAsync("/backups");
    Console.WriteLine($"bucket holds: {string.Join(", ", page.Entries.Select(e => e.Name).Where(n => n != ".keep").DefaultIfEmpty("nothing"))}");
}

return 0;

// The app's uploader: start, one put per part, complete with each part's number and ETag. When a step
// is refused it aborts the upload.
async Task<bool> UploadInParts(string key, byte[] data, int partSize)
{
    (int status, string body, _) = await Send(HttpMethod.Post, $"{key}?uploads", null);
    string id = Regex.Match(body, "<UploadId>([^<]+)</UploadId>").Groups[1].Value;
    Console.WriteLine($"initiate: {status}");
    var complete = new StringBuilder("<CompleteMultipartUpload>");
    for (int n = 1, offset = 0; offset < data.Length; n++, offset += partSize)
    {
        byte[] part = data[offset..Math.Min(offset + partSize, data.Length)];
        (status, body, string? etag) = await Send(HttpMethod.Put, $"{key}?partNumber={n}&uploadId={id}", part);
        Console.WriteLine($"part {n}: {status}");
        if (status != 200) return await Abort(key, id, body);
        complete.Append($"<Part><PartNumber>{n}</PartNumber><ETag>{etag}</ETag></Part>");
    }

    complete.Append("</CompleteMultipartUpload>");
    (status, body, _) = await Send(HttpMethod.Post, $"{key}?uploadId={id}", Encoding.UTF8.GetBytes(complete.ToString()));
    Console.WriteLine($"complete: {status}");
    return status == 200 || await Abort(key, id, body);
}

async Task<bool> Abort(string key, string id, string refusal)
{
    Console.WriteLine($"refused: {Code(refusal)}");
    (int status, _, _) = await Send(HttpMethod.Delete, $"{key}?uploadId={id}", null);
    Console.WriteLine($"abort: {status}");
    return false;
}

// One signed request. Over HTTPS an S3 refusal is an ordinary response with an XML Error body.
async Task<(int Status, string Body, string? ETag)> Send(HttpMethod method, string path, byte[]? body)
{
    var request = new CurlRequest(method, Url + path) { Network = network, Tls = trust, Authentication = signing };
    if (body is not null) request = request with { Body = body };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream stream = response.Body;
    string text = await new StreamReader(stream).ReadToEndAsync();
    string? etag = response.Headers.FirstOrDefault(h => h.Key.Equals("ETag", StringComparison.OrdinalIgnoreCase)).Value;
    return (response.StatusCode, text, etag);
}

static string Code(string body) => Regex.Match(body, "<Code>([^<]+)</Code>").Groups[1].Value;
