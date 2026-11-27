// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// One test run against an in-process S3 endpoint with a random seed: the app's multipart upload,
// then what the server stored. The seed is printed first, so a failed run's log says how to repeat it.
// Run: dotnet run rerun.cs [-- <seed> [probe] | nonces | bad-seed]
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;

if (args is ["nonces"])
{
    return await CompareNonces();
}

if (args is ["bad-seed"])
{
    try
    {
        await using SurlServer refused = await SurlServer.StartAsync(Options(-1));
        Console.WriteLine("started");
    }
    catch (Exception failure)
    {
        Console.WriteLine($"seed -1: {failure.GetType().Name}");
    }

    return 2;
}

// No seed given: draw one. Either way, print it before anything else.
long seed = args.Length > 0 ? long.Parse(args[0]) : RandomNumberGenerator.GetInt32(int.MaxValue);
Console.WriteLine($"seed={seed}");

await using SurlServer server = await SurlServer.StartAsync(Options(seed));
string endpoint = $"http://127.0.0.1:{server.BoundUrl.Port}/";

// A readiness check that connects is one more exchange before the app's.
if (args is [_, "probe"])
{
    await using CurlClient probe = CurlClient.Create();
    await (await probe.SendAsync(new CurlRequest(HttpMethod.Get, endpoint), CancellationToken.None)).Body.DisposeAsync();
}

int result = await Upload(endpoint);

// What the server recorded: the object, if the upload completed.
try
{
    SurlFileContents stored = await server.Files.ReadAsync("/photos/big.bin");
    Console.WriteLine($"server holds big.bin: {Encoding.UTF8.GetString(stored.Bytes.Span).TrimEnd()}");
}
catch (SurlNotFoundException)
{
    Console.WriteLine("server holds no big.bin");
}

return result;

// The S3 endpoint the app needs, served as plain HTTP from memory, with the seed.
static SurlServerOptions Options(long seed)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "s3://127.0.0.1:0" },
        RandomSeed = seed,
        AllowUploads = true,
        AdditionalArguments = { "--s3-plaintext" },
    };
    options.Accounts.Add(new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret"));
    options.Files["photos/readme.txt"] = "the bucket the app uploads to\n"u8.ToArray();
    return options;
}

// The app under test: a multipart upload of big.bin into the photos bucket.
static async Task<int> Upload(string endpoint)
{
    await using CurlClient client = CurlClient.Create();
    var login = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = "s3cret", AwsSigV4 = "aws:amz:us-east-1:s3" };
    string url = $"{endpoint}photos/big.bin";

    string id = Regex.Match(await Send(HttpMethod.Post, $"{url}?uploads", null), "<UploadId>([0-9a-f]+)</UploadId>").Groups[1].Value;
    Console.WriteLine($"upload {id}");

    // The bug the test run trips over, standing in for your app's: it uses the upload ID as a
    // name, and a name may not begin with a digit. Only some IDs bring it out.
    if (char.IsAsciiDigit(id[0]))
    {
        Console.WriteLine($"failed: upload {id} cannot be named");
        return 1;
    }

    CurlResponse part = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{url}?partNumber=1&uploadId={id}")
    {
        Authentication = login,
        Body = CurlContent.FromBytes("part one\n"u8.ToArray()),
    }, CancellationToken.None);
    await part.Body.DisposeAsync();
    string etag = part.Headers.First(header => header.Key.Equals("ETag", StringComparison.OrdinalIgnoreCase)).Value;

    await Send(HttpMethod.Post, $"{url}?uploadId={id}",
        $"<CompleteMultipartUpload><Part><PartNumber>1</PartNumber><ETag>{etag}</ETag></Part></CompleteMultipartUpload>");
    Console.WriteLine("stored big.bin");
    return 0;

    async Task<string> Send(HttpMethod method, string target, string? body)
    {
        var request = new CurlRequest(method, target) { Authentication = login };
        if (body is not null)
        {
            request = request with { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(body)) };
        }

        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream stream = response.Body;
        return await new StreamReader(stream).ReadToEndAsync();
    }
}

// Two HTTP servers with the same seed and one account: their Digest nonces still differ.
static async Task<int> CompareNonces()
{
    async Task<string> Nonce()
    {
        var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RandomSeed = 3 };
        options.Accounts.Add(new SurlAccount("alice", "s3cret"));
        await using SurlServer server = await SurlServer.StartAsync(options);
        await using CurlClient client = CurlClient.Create();
        CurlResponse challenge = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}a.txt"), CancellationToken.None);
        await challenge.Body.DisposeAsync();
        string header = challenge.Headers.First(h => h.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase)).Value;
        return Regex.Match(header, "nonce=\"([0-9a-f]+)\"").Groups[1].Value;
    }

    string first = await Nonce(), second = await Nonce();
    Console.WriteLine($"same seed, same nonce: {first == second}");
    return 0;
}
