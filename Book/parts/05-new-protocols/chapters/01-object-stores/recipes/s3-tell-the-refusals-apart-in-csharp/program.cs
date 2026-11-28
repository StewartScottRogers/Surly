// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Produces each S3 refusal on purpose with Curl.Api against two Surl.Api servers and names it from the
// exception, in one process. Run: dotnet run program.cs
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// Two servers on the same object, each in memory. main has one account and nothing loosened; open
// also allows anonymous callers and uploads. server.pem and server.key are in the folder.
Task<SurlServer> Start(bool anonymous)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "s3://127.0.0.1:0" },
        CertificateFile = "server.pem",
        KeyFile = "server.key",
        AllowAnonymous = anonymous,
        AllowUploads = anonymous,
    };
    options.Accounts.Add(new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret"));
    options.Files["photos/hello.txt"] = "hello from surl\n"u8.ToArray();
    return SurlServer.StartAsync(options);
}

await using SurlServer main = await Start(anonymous: false);
await using SurlServer open = await Start(anonymous: true);

await using CurlClient client = CurlClient.Create();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
CurlAuthenticationOptions? Login(string key, string secret) => new() { UserName = key, Password = secret };

await Show("signed", main, "s3://photos/hello.txt", Login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
await Show("unsigned", main, "s3://photos/hello.txt", null);
await Show("unknown key", main, "s3://photos/hello.txt", Login("AKIAOTHEREXAMPLE", "s3cret"));
await Show("wrong secret", main, "s3://photos/hello.txt", Login("AKIAIOSFODNN7EXAMPLE", "wrong"));
await Show("signed put", main, "s3://photos/note.txt", Login("AKIAIOSFODNN7EXAMPLE", "s3cret"), "a note\n");
await Show("signed listing", main, "s3://photos/", Login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
await Show("missing key", main, "s3://photos/missing.txt", Login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
await Show("missing bucket", main, "s3://videos/hello.txt", Login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
await Show("anonymous read", open, "s3://photos/hello.txt", null);
await Show("anonymous put", open, "s3://photos/note.txt", null, "a note\n");
await Show("anonymous unknown key", open, "s3://photos/hello.txt", Login("AKIAOTHEREXAMPLE", "s3cret"));
await Show("anonymous wrong secret", open, "s3://photos/hello.txt", Login("AKIAIOSFODNN7EXAMPLE", "wrong"));
return 0;

// One request, a get, or a put when there is a body. An answer prints its status and body; a refusal throws
// CurlException, and the program names it from the status and S3 error code in the message, which is how a
// client explains why S3 said no.
async Task Show(string label, SurlServer server, string url, CurlAuthenticationOptions? login, string? upload = null)
{
    var request = new CurlRequest(upload is null ? HttpMethod.Get : HttpMethod.Put, url)
    {
        Network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] },
        Tls = trust,
        Authentication = login ?? new CurlAuthenticationOptions(),
        Body = upload is null ? null : System.Text.Encoding.UTF8.GetBytes(upload),
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        Console.WriteLine($"{label}: {response.StatusCode} {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
    }
    catch (CurlException failure)
    {
        Match named = Regex.Match(failure.Message, @"returned error (\d+): (\w+): (.*)");
        Console.WriteLine($"{label}: {failure.Code} {named.Groups[1]} {named.Groups[2]} ({named.Groups[3].Value.TrimEnd()})");
    }
}
