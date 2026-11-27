// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A CI test against an in-process server: three fetches over HTTPS and FTPS. When one fails, the
// test keeps the server's verbose log in ci-logs/surl.log for the job to upload, and fails. The
// log already holds no password or token: the server writes **** in their place.
// Run: dotnet run keep-logs.cs [-- <ftps password>]   (the right one, s3cret, by default)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Output;

string ftpsPassword = args.Length > 0 ? args[0] : "s3cret";

// The server: a password account and a Bearer token (the empty user name), over HTTPS and FTPS,
// logging every exchange event and every line sent and received to a writer in memory.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0", "ftps://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("app", "s3cret"), new SurlAccount("", "tok-7f3a9c") },
    Log = log,
    LogLevel = LogLevel.Verbose,
};
options.Files["report.csv"] = "id,total\n1,40\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
string https = server.BoundUrls[0].ToString(), ftps = server.BoundUrls[1].ToString();

// The client trusts exactly the certificate the server made.
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};

// The tests: the first failure ends the run.
bool passed =
    await Fetch("https password", https, new CurlAuthenticationOptions { UserName = "app", Password = "s3cret", HttpAuthentication = HttpAuthenticationMethods.Basic })
    && await Fetch("https token", https, new CurlAuthenticationOptions { XOAuth2Bearer = "tok-7f3a9c", HttpAuthentication = HttpAuthenticationMethods.Bearer })
    && await Fetch("ftps password", ftps, new CurlAuthenticationOptions { UserName = "app", Password = ftpsPassword });
if (passed)
{
    return 0;
}

// The run failed: keep the log as the server wrote it.
await server.StopAsync();
Directory.CreateDirectory("ci-logs");
File.WriteAllText("ci-logs/surl.log", log.ToString());
Console.WriteLine("kept ci-logs/surl.log");
return 1;

// One fetch of report.csv with the given login; prints the outcome and says whether it passed.
async Task<bool> Fetch(string label, string baseUrl, CurlAuthenticationOptions login)
{
    try
    {
        await using CurlClient client = CurlClient.Create();
        var request = new CurlRequest(HttpMethod.Get, $"{baseUrl}report.csv") { Tls = trust, Authentication = login };
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        Console.WriteLine($"{label}: {response.StatusCode}");
        return response.StatusCode is 0 or 200;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: {(int)failure.Code} {failure.Code}");
        return false;
    }
}
