// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves S3 as plain HTTP with Surl.Api and reaches it with Curl.Api over http://, signed, in one process.
// Run: dotnet run program.cs
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

// Two servers on the same object, in memory, each one account. AdditionalArguments takes any surl option as
// surl's own words, here --s3-plaintext: no certificate is needed. writable also allows uploads. main's log
// is kept to read the start-up warning from.
var log = new StringWriter();
Task<SurlServer> Start(bool uploads, TextWriter? writer)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "s3://127.0.0.1:0" },
        AllowUploads = uploads,
        AdditionalArguments = { "--s3-plaintext" },
        Log = writer,
        LogLevel = LogLevel.Info,
    };
    options.Accounts.Add(new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret"));
    options.Files["photos/hello.txt"] = "hello from surl\n"u8.ToArray();
    return SurlServer.StartAsync(options);
}

await using SurlServer main = await Start(uploads: false, log);
await using SurlServer writable = await Start(uploads: true, null);

// The start-up warning goes to the log, byte for byte as surl writes it.
Console.WriteLine($"warning: {log.ToString().Split('\n').First(line => line.Contains("--s3-plaintext")).Trim()}");

// Without --s3-plaintext the same listen URL, with no certificate, is refused: surl would exit 58.
try
{
    await using var refused = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "s3://127.0.0.1:0" } });
}
catch (SurlServerStartException failure)
{
    Console.WriteLine($"no certificate: exit {(int)failure.ExitCode}, {Regex.Match(failure.Lines[0], @"^\(\d+\) s3://\S+ needs a certificate").Value}");
}

await using CurlClient client = CurlClient.Create();
CurlAuthenticationOptions Login(string key, string secret) => new()
{
    UserName = key,
    Password = secret,
    AwsSigV4 = "aws:amz:us-east-1:s3",
};

// An http:// URL on the loopback address, path style: the bucket is the first segment of the path.
string Url(SurlServer server, string path) => $"http://127.0.0.1:{server.BoundUrl.Port}/{path}";

await Show("signed", main, Url(main, "photos/hello.txt"), Login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
// An SDK that puts the bucket in the host name: ConnectTo sends the connection to the server.
await Show("virtual host", main, "http://photos.s3.us-east-1.amazonaws.com/hello.txt", Login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
await Show("unsigned", main, Url(main, "photos/hello.txt"), new CurlAuthenticationOptions());
await Show("wrong secret", main, Url(main, "photos/hello.txt"), Login("AKIAIOSFODNN7EXAMPLE", "wrong"));
await Show("unknown key", main, Url(main, "photos/hello.txt"), Login("AKIAOTHEREXAMPLE", "s3cret"));
await Show("missing key", main, Url(main, "photos/missing.txt"), Login("AKIAIOSFODNN7EXAMPLE", "s3cret"));
await Show("uploads off", main, Url(main, "photos/up.txt"), Login("AKIAIOSFODNN7EXAMPLE", "s3cret"), "uploaded over plain http\n");
await Show("uploads on", writable, Url(writable, "photos/up.txt"), Login("AKIAIOSFODNN7EXAMPLE", "s3cret"), "uploaded over plain http\n");

SurlFileContents stored = await writable.Files.ReadAsync("/photos/up.txt");
Console.WriteLine($"stored: {System.Text.Encoding.UTF8.GetString(stored.Bytes.Span).TrimEnd()}");

// curl's own s3:// URL is TLS from the first byte, so it cannot talk to the plain-HTTP listener.
try
{
    var tls = new CurlRequest(HttpMethod.Get, "s3://photos/hello.txt")
    {
        Network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{main.BoundUrl.Port}"] },
        Authentication = Login("AKIAIOSFODNN7EXAMPLE", "s3cret"),
    };
    await client.SendAsync(tls, CancellationToken.None);
    Console.WriteLine("s3 url: served");
}
catch (CurlException failure)
{
    Console.WriteLine($"s3 url: {failure.Code}");
}
return 0;

// One request, a get, or a put when there is a body. Over http:// a refusal is an ordinary response, with the
// status and the S3 error code in its XML body, not an exception.
async Task Show(string label, SurlServer server, string url, CurlAuthenticationOptions login, string? upload = null)
{
    var request = new CurlRequest(upload is null ? HttpMethod.Get : HttpMethod.Put, url)
    {
        Network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] },
        Authentication = login,
        Body = upload is null ? null : System.Text.Encoding.UTF8.GetBytes(upload),
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = (await new StreamReader(body).ReadToEndAsync()).TrimEnd();
    Match code = Regex.Match(text, @"<Code>(\w+)</Code>");
    Console.WriteLine(code.Success ? $"{label}: {response.StatusCode} {code.Groups[1]}" : $"{label}: {response.StatusCode} {text}");
}
