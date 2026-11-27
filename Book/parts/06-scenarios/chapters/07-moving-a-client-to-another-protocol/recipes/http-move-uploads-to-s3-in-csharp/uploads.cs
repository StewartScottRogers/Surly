// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of moving an app's uploads from HTTP PUT to S3: one in-process server serves one store over
// HTTPS and S3, the old client and the new one each upload, and the store and the recorded logins are read.
// Run: dotnet run uploads.cs [-- across|codes|refused|cutover]
//   (no argument) one upload each way, the bucket listed, the store and the logins read back
//   across        each upload read back over the other protocol
//   codes         the status of a first upload and of a replacing one, over each protocol
//   refused       an HTTP PUT with no login, an unsigned S3 put, and one signed with the wrong secret
//   cutover       a server that takes signed requests only
// server.pem and server.key come from setup: in recipe.yml.
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "move";
byte[] report = "region,total\nnorth,1200\nsouth,950\n"u8.ToArray();
byte[] summary = "total\n2150\n"u8.ToArray();
const string Key = "AKIAIOSFODNN7EXAMPLE";

// The server: one store, an s3:// listener for the new client and an https:// one for the old, one key
// pair for both, uploads on. "cutover" takes signed requests only.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0", "https://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount(Key, "s3cret") },
    AllowUploads = true,
    ListDirectories = true,
    RecordExchanges = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
};
if (which == "cutover")
{
    options.AuthenticationMethods = new List<string> { "aws-sigv4" };
}

options.Files["uploads/readme.txt"] = "uploads land here\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
string https = server.BoundUrls[1].ToString();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "move":
        await Send("old client, HTTP PUT report.csv", HttpMethod.Put, $"{https}uploads/report.csv", report, "s3cret");
        await Send("new client, S3 put summary.csv", HttpMethod.Put, "s3://uploads/summary.csv", summary, "s3cret");
        Console.Write(await Send(null, HttpMethod.Get, "s3://uploads/", null, "s3cret"));
        Console.WriteLine($"store: report.csv {await Stored("report.csv", report)}, summary.csv {await Stored("summary.csv", summary)}");
        Console.WriteLine($"uploads logged: {string.Join(", ", log.ToString().Split('\n').Where(l => l.Contains("* PUT /")).Select(l => l[(l.IndexOf("PUT /") + 4)..l.IndexOf(',')]))}");
        foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
        {
            Console.WriteLine($"login judged: {login.Scheme} {login.Mechanism} {login.Account}");
        }

        break;
    case "across":
        await Send(null, HttpMethod.Put, $"{https}uploads/report.csv", report, "s3cret");
        await Send(null, HttpMethod.Put, "s3://uploads/summary.csv", summary, "s3cret");
        string overS3 = await Send(null, HttpMethod.Get, "s3://uploads/report.csv", null, "s3cret") ?? "";
        string overHttps = await Send(null, HttpMethod.Get, $"{https}uploads/summary.csv", null, "s3cret") ?? "";
        Console.WriteLine($"HTTP upload read over S3: {(overS3 == Encoding.UTF8.GetString(report) ? "identical" : "DIFFERENT")}");
        Console.WriteLine($"S3 upload read over HTTPS: {(overHttps == Encoding.UTF8.GetString(summary) ? "identical" : "DIFFERENT")}");
        break;
    case "codes":
        await Send("HTTP PUT, new", HttpMethod.Put, $"{https}uploads/again.csv", report, "s3cret");
        await Send("HTTP PUT, replacing", HttpMethod.Put, $"{https}uploads/again.csv", report, "s3cret");
        await Send("S3 put, new", HttpMethod.Put, "s3://uploads/twice.csv", summary, "s3cret");
        await Send("S3 put, replacing", HttpMethod.Put, "s3://uploads/twice.csv", summary, "s3cret");
        break;
    case "refused":
        await Send("HTTP PUT, no login", HttpMethod.Put, $"{https}uploads/nobody.csv", report, null);
        await Send("S3 put, unsigned", HttpMethod.Put, "s3://uploads/nobody.csv", summary, null);
        await Send("S3 put, wrong secret", HttpMethod.Put, "s3://uploads/nobody.csv", summary, "wrong");
        Console.WriteLine($"store: nobody.csv {await Stored("nobody.csv", report)}");
        break;
    case "cutover":
        await Send("old client, HTTP PUT report.csv", HttpMethod.Put, $"{https}uploads/report.csv", report, "s3cret");
        Console.WriteLine($"store: report.csv {await Stored("report.csv", report)}");
        await Send("new client, S3 put report.csv", HttpMethod.Put, "s3://uploads/report.csv", report, "s3cret");
        Console.WriteLine($"store: report.csv {await Stored("report.csv", report)}");
        break;
}

return 0;

// One request: an https:// URL logs in with the key pair as user and password (Basic over TLS), an s3://
// URL is signed with it and sent to the server's s3 port. A label prints the status or the refusal; the
// body comes back as text.
async Task<string?> Send(string? label, HttpMethod method, string url, byte[]? body, string? secret)
{
    var request = new CurlRequest(method, url)
    {
        Tls = trust,
        Network = url.StartsWith("s3:") ? new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] } : null,
        Authentication = secret is null ? null : new CurlAuthenticationOptions { UserName = Key, Password = secret },
        Body = body,
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream stream = response.Body;
        string text = await new StreamReader(stream).ReadToEndAsync();
        if (label is not null)
        {
            Console.WriteLine($"{label}: {response.StatusCode}");
        }

        return text;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: {(int)failure.Code} {failure.Code} ({failure.Message})");
        return null;
    }
}

// What the store holds under /uploads: the same bytes, other bytes, or nothing.
async Task<string> Stored(string name, byte[] expected)
{
    try
    {
        byte[] held = (await server.Files.ReadAsync($"/uploads/{name}")).Bytes.ToArray();
        return held.AsSpan().SequenceEqual(expected) ? "identical" : "DIFFERENT";
    }
    catch (SurlNotFoundException)
    {
        return "absent";
    }
}
