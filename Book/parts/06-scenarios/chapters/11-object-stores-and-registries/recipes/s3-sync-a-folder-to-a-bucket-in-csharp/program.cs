// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a folder sync to an S3 bucket: Surl.Api serves the bucket in process, Curl.Api puts the files of a
// folder, lists the bucket, and after one file changes puts only that one, which the listing then proves.
// server.pem and server.key are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   sync (default)      sync the folder, list the bucket, compare the store with the folder
//   one-put-per-file    count the objects the server stored for the sync
//   listing-with-sizes  the signed listing's size and ETag of each key
//   resync-one-file     change one file and sync again
//   one-more-put        count the objects stored after the second sync
//   listing-proves-it   the listing before and after the second sync
//   changed-file-reads-back   get the changed object
//   nothing-changed     sync twice with no change between
//   wrong-secret        a put signed with the wrong secret
//   uploads-off         a put to a bucket with AllowUploads off
//   nothing-arrived     a get after that refused put
//   listing-off         a listing of a bucket with ListDirectories off
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Http;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "sync";

// The folder to sync: three small reports.
Directory.CreateDirectory("site");
File.WriteAllText("site/a.csv", "region,total\nnorth,1200\n");
File.WriteAllText("site/b.csv", "region,total\nsouth,950\n");
File.WriteAllText("site/c.csv", "region,total\neast,700\n");

// The bucket: one key pair, and an object that was there before the sync.
var options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret") },
    AllowUploads = which is not ("uploads-off" or "nothing-arrived"),
    ListDirectories = which != "listing-off",
    RecordExchanges = true,
};
options.Files["reports/old.csv"] = "stale\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create();
var network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] };
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
string secret = which == "wrong-secret" ? "wrong" : "s3cret";

switch (which)
{
    case "one-put-per-file":
        await Sync();
        Console.WriteLine($"puts so far: {Puts()}");
        break;

    case "listing-with-sizes":
        await Sync();
        foreach ((string key, (long size, string etag)) in await Remote()) Console.WriteLine($"{key} {size} {etag}");
        break;

    case "resync-one-file" or "one-more-put":
        await Sync();
        File.WriteAllText("site/b.csv", "region,total\nsouth,19500\n");
        Console.WriteLine($"changed: {string.Join(" ", await Sync())}");
        Console.WriteLine(which == "one-more-put" ? $"puts so far: {Puts()}" : $"listing: {await ListNames()}");
        break;

    case "listing-proves-it":
        await Sync();
        var before = await Remote();
        File.WriteAllText("site/b.csv", "region,total\nsouth,19500\n");
        await Sync();
        foreach ((string key, (long size, string etag)) in await Remote())
        {
            (long oldSize, string oldEtag) = before[key];
            Console.WriteLine(oldEtag == etag ? $"{key}: unchanged, {size} bytes" : $"{key}: {oldSize} -> {size} bytes, new ETag");
        }

        break;

    case "changed-file-reads-back":
        await Sync();
        File.WriteAllText("site/b.csv", "region,total\nsouth,19500\n");
        await Sync();
        Console.Write(await Text("s3://reports/b.csv"));
        break;

    case "nothing-changed":
        await Sync();
        Console.WriteLine($"second sync put: {(await Sync() is { Count: > 0 } again ? string.Join(" ", again) : "nothing")}");
        Console.WriteLine($"puts so far: {Puts()}");
        break;

    case "wrong-secret" or "uploads-off":
        Console.WriteLine($"put a.csv: {await Failure(() => Put("a.csv"))}");
        break;

    case "nothing-arrived":
        Console.WriteLine($"put a.csv: {await Failure(() => Put("a.csv"))}");
        Console.WriteLine($"get a.csv: {await Failure(() => Text("s3://reports/a.csv"))}");
        break;

    case "listing-off":
        Console.WriteLine($"list: {await Failure(ListNames)}");
        break;

    default:
        Console.WriteLine($"synced: {string.Join(" ", await Sync())}");
        Console.WriteLine($"listing: {await ListNames()}");
        foreach (string file in Directory.GetFiles("site").Order(StringComparer.Ordinal))
        {
            SurlFileContents stored = await server.Files.ReadAsync($"/reports/{Path.GetFileName(file)}");
            Console.WriteLine($"{Path.GetFileName(file)} in the store matches the folder: {stored.Bytes.Span.SequenceEqual(File.ReadAllBytes(file))}");
        }

        break;
}

return 0;

// A sync: put each file of site/ whose MD5 is not the ETag of the object of that name. Returns what it put.
async Task<List<string>> Sync()
{
    Dictionary<string, (long Size, string ETag)> remote = await Remote();
    var put = new List<string>();
    foreach (string file in Directory.GetFiles("site").Order(StringComparer.Ordinal))
    {
        string name = Path.GetFileName(file);
        string md5 = Convert.ToHexStringLower(MD5.HashData(File.ReadAllBytes(file)));
        if (remote.TryGetValue(name, out var known) && known.ETag == md5) continue;
        await Put(name);
        put.Add(name);
    }

    return put;
}

// A put of one file of site/ through s3://, signed from the login alone.
async Task Put(string name)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"s3://reports/{name}")
    {
        Network = network,
        Tls = trust,
        Authentication = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = secret },
        Body = CurlContent.FromBytes(File.ReadAllBytes($"site/{name}")),
    }, CancellationToken.None);
    await using Stream body = response.Body;
}

// A get of a URL through the server, as text.
async Task<string> Text(string url)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url)
    {
        Network = network,
        Tls = trust,
        Authentication = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = secret },
    }, CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body).ReadToEndAsync();
}

// The bucket's names, from the s3:// listing: one a line.
async Task<string> ListNames() => string.Join(" ", (await Text("s3://reports/")).Split('\n', StringSplitOptions.RemoveEmptyEntries));

// The size and ETag of each key, from ListObjectsV2 signed with AWS Signature Version 4 over HTTPS to the
// bucket's host name. The signed request is HTTP/1.1.
async Task<Dictionary<string, (long Size, string ETag)>> Remote()
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, "https://reports.s3.us-east-1.amazonaws.com/?list-type=2")
    {
        Network = network,
        Tls = trust,
        Http = new CurlHttpOptions { HttpVersion = HttpVersion.Http11 },
        Authentication = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = secret, AwsSigV4 = "aws:amz:us-east-1:s3" },
    }, CancellationToken.None);
    await using Stream body = response.Body;
    string xml = await new StreamReader(body).ReadToEndAsync();
    return Regex.Matches(xml, "<Key>([^<]*)</Key>.*?<ETag>\"([^\"]*)\"</ETag><Size>(\\d+)</Size>")
        .ToDictionary(m => m.Groups[1].Value, m => (long.Parse(m.Groups[3].Value), m.Groups[2].Value));
}

// How many objects the server has stored for requests, as the store's own change events tell it. The object
// that was there before the sync was seeded, not put, so it has no request behind it.
int Puts() => server.FindEvents(new SurlEventQuery { Store = StoreKind.Content }).Count(e => e.ExchangeId is not null);

// What a refusal says: the exception's code and, from its message, the status and error code.
async Task<string> Failure(Func<Task> call)
{
    try
    {
        await call();
        return "served";
    }
    catch (CurlException failure)
    {
        Match named = Regex.Match(failure.Message, @"returned error (\d+): (\w+)");
        return $"{failure.Code} {named.Groups[1]} {named.Groups[2]}";
    }
}
