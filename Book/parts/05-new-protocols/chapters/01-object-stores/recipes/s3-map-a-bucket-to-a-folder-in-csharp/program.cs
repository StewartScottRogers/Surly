// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Maps a bucket onto a folder of an in-process Surl.Api server and reads, lists and writes it with
// Curl.Api. Run: dotnet run program.cs
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// One server, an s3 and an https listener on one store held in memory. The folder gallery is the bucket
// pics, and other is a folder nobody mapped. server.pem and server.key are in the folder.
var options = new SurlServerOptions
{
    ListenUrls = { "s3://127.0.0.1:0", "https://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    AllowUploads = true,
    ListDirectories = true,
};
options.Buckets.Add("pics=/gallery");
options.Accounts.Add(new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret"));
options.Files["gallery/sunset.txt"] = "sunset\n"u8.ToArray();
options.Files["gallery/2026/jan.txt"] = "january\n"u8.ToArray();
options.Files["other/o.txt"] = "other\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
string s3Url = server.BoundUrls[0].ToString(), httpsUrl = server.BoundUrls[1].ToString();

await using CurlClient client = CurlClient.Create();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
var signed = new CurlAuthenticationOptions { UserName = "AKIAIOSFODNN7EXAMPLE", Password = "s3cret" };

// An s3:// URL names the bucket as its host, and ConnectTo sends the connection to the server.
await Show("pics get", HttpMethod.Get, "s3://pics/sunset.txt");
await Show("pics list", HttpMethod.Get, "s3://pics/");
await Show("gallery get", HttpMethod.Get, "s3://gallery/sunset.txt");
await Show("other get", HttpMethod.Get, "s3://other/o.txt");
await Show("no bucket", HttpMethod.Get, "s3://videos/sunset.txt");
await Show("https gallery", HttpMethod.Get, $"{httpsUrl}gallery/sunset.txt");
await Show("https pics", HttpMethod.Get, $"{httpsUrl}pics/sunset.txt");

// A put into the bucket pics writes the file into the folder gallery, read back from the store.
await Show("pics put", HttpMethod.Put, "s3://pics/up.txt", CurlContent.FromStream(new MemoryStream("uploaded\n"u8.ToArray()), 9));
SurlFileContents landed = await server.Files.ReadAsync("/gallery/up.txt");
Console.WriteLine($"folder holds: [{Encoding.UTF8.GetString(landed.Bytes.Span).TrimEnd()}]");
await Show("gallery get", HttpMethod.Get, "s3://gallery/up.txt");

// A bucket mapped onto a folder that is not on disk stops the start. DataDirectory is --directory.
var broken = new SurlServerOptions { ListenUrls = { "s3://127.0.0.1:0" }, DataDirectory = "spare", CertificateFile = "server.pem", KeyFile = "server.key" };
broken.Buckets.Add("pics=/nowhere");
broken.Accounts.Add(new SurlAccount("AKIAIOSFODNN7EXAMPLE", "s3cret"));
try
{
    await using SurlServer never = await SurlServer.StartAsync(broken);
}
catch (SurlServerStartException failure)
{
    Console.WriteLine($"missing folder: SurlServerStartException {(int)failure.ExitCode}: {failure.Lines[0]}");
}

return 0;

// One request, signed. A listing's body is the keys, one to a line. A refusal throws CurlException.
async Task Show(string label, HttpMethod method, string url, CurlContent? content = null)
{
    var request = new CurlRequest(method, url)
    {
        Network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrls[0].Port}"] },
        Tls = trust,
        Authentication = signed,
        Body = content,
    };
    if (url.StartsWith("https"))
    {
        request = request with { Network = null };
    }

    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        Console.WriteLine($"{label}: {response.StatusCode} [{string.Join(", ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries))}]");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}: {failure.Message.Split('\n')[0]}");
    }
}
