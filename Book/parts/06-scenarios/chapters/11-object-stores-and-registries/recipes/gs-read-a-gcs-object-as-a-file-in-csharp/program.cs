// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that objects written through Cloud Storage can be read as files: Surl.Api serves one store as gs://
// and https://, Curl.Api writes through gs:// and reads over https://. server.pem and server.key are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   write-and-read (default)  put an object through gs://, read it over https://, read the stored file
//   no-trailing-newline       an object whose last byte is not a newline
//   nested-name               an object name with slashes
//   file-is-an-object         an object that was already in the store, read through gs://
//   put-with-the-token        a put with the OAuth token, read with the HMAC key pair over https://
//   content-type              the Content-Type of an object read over https://
//   https-needs-a-login       a read over https:// with no login
//   wrong-secret              a put signed with the wrong secret
//   refused-is-not-written    the same refused put, then a read of the name
//   missing-object            a get of a name that holds nothing
//   uploads-off               a put to a server with AllowUploads off
//   uploads-off-reads         reads from that server
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "write-and-read";

// One store, served as Cloud Storage and as HTTPS, with two logins: an HMAC key pair and an OAuth token.
var options = new SurlServerOptions
{
    ListenUrls = { "gs://127.0.0.1:0", "https://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    AllowUploads = !which.StartsWith("uploads-off"),
};
options.Accounts.Add(new SurlAccount("GOOGTESTKEY", "s3cret"));
options.Accounts.Add(new SurlAccount("", "ya29.t0ken"));
options.Files["photos/seed.txt"] = "already here\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

// BoundUrls holds one address per listen URL, in order: gs:// first, https:// second.
int gsPort = server.BoundUrls[0].Port;
string https = $"https://127.0.0.1:{server.BoundUrls[1].Port}";

await using CurlClient client = CurlClient.Create();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
var viaServer = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{gsPort}"] };
CurlAuthenticationOptions Hmac(string secret = "s3cret") => new() { UserName = "GOOGTESTKEY", Password = secret };
CurlAuthenticationOptions Token() => new() { XOAuth2Bearer = "ya29.t0ken", HttpAuthentication = HttpAuthenticationMethods.Bearer };
CurlAuthenticationOptions Basic() => new() { UserName = "GOOGTESTKEY", Password = "s3cret", HttpAuthentication = HttpAuthenticationMethods.Basic };

switch (which)
{
    case "no-trailing-newline":
        byte[] tail = "line one\nline two"u8.ToArray();
        await Put("gs://photos/tail.txt", tail, Hmac());
        (int _, byte[] got, _) = await Get($"{https}/photos/tail.txt", Token());
        Console.WriteLine($"read {got.Length} bytes, identical to the upload: {got.AsSpan().SequenceEqual(tail)}");
        break;

    case "nested-name":
        await Put("gs://photos/2026/q1/report.txt", "q1 totals\n"u8.ToArray(), Hmac());
        (_, got, _) = await Get($"{https}/photos/2026/q1/report.txt", Token());
        Console.Write($"read: {Encoding.UTF8.GetString(got)}");
        Console.WriteLine($"stored at /photos/2026/q1/report.txt: {Encoding.UTF8.GetString((await server.Files.ReadAsync("/photos/2026/q1/report.txt")).Bytes.Span).TrimEnd()}");
        break;

    case "file-is-an-object":
        Console.WriteLine(await Show("get", () => Get("gs://photos/seed.txt", Hmac())));
        break;

    case "put-with-the-token":
        Console.WriteLine(await Show("put", () => Put("gs://photos/by-token.txt", "hello from gcs\n"u8.ToArray(), Token())));
        Console.WriteLine(await Show("read", () => Get($"{https}/photos/by-token.txt", Basic())));
        break;

    case "content-type":
        (_, _, string type) = await Get($"{https}/photos/seed.txt", Token());
        Console.WriteLine($"Content-Type: {type}");
        break;

    case "https-needs-a-login":
        (int status, _, _) = await Get($"{https}/photos/seed.txt", new CurlAuthenticationOptions());
        Console.WriteLine($"read with no login: {status}");
        break;

    case "wrong-secret":
        Console.WriteLine(await Show("put", () => Put("gs://photos/refused.txt", "hello from gcs\n"u8.ToArray(), Hmac("wrong"))));
        break;

    case "refused-is-not-written":
        Console.WriteLine(await Show("put", () => Put("gs://photos/refused.txt", "hello from gcs\n"u8.ToArray(), Hmac("wrong"))));
        (status, _, _) = await Get($"{https}/photos/refused.txt", Token());
        Console.WriteLine($"read over https: {status}");
        try
        {
            await server.Files.ReadAsync("/photos/refused.txt");
            Console.WriteLine("store: the object is there");
        }
        catch (SurlNotFoundException)
        {
            Console.WriteLine("store: no such object");
        }

        break;

    case "missing-object":
        Console.WriteLine(await Show("get", () => Get("gs://photos/missing.txt", Hmac())));
        break;

    case "uploads-off":
        Console.WriteLine(await Show("put", () => Put("gs://photos/note.txt", "hello from gcs\n"u8.ToArray(), Hmac())));
        break;

    case "uploads-off-reads":
        Console.WriteLine(await Show("gs get", () => Get("gs://photos/seed.txt", Hmac())));
        Console.WriteLine(await Show("https get", () => Get($"{https}/photos/seed.txt", Basic())));
        break;

    default:
        // The write goes through gs:// with the HMAC key pair, the read over https:// with the token.
        Console.WriteLine(await Show("put", () => Put("gs://photos/note.txt", "hello from gcs\n"u8.ToArray(), Hmac())));
        Console.WriteLine(await Show("read", () => Get($"{https}/photos/note.txt", Token())));
        SurlFileContents stored = await server.Files.ReadAsync("/photos/note.txt");
        Console.WriteLine($"stored: {Encoding.UTF8.GetString(stored.Bytes.Span).TrimEnd()}");
        break;
}

return 0;

// A request: gs:// goes to Google's host name, so it is routed to the server; https:// names the server.
CurlRequest Build(HttpMethod method, string url, CurlAuthenticationOptions login, byte[]? body = null) => new(method, url)
{
    Network = url.StartsWith("gs:") ? viaServer : null,
    Tls = trust,
    Authentication = login,
    Body = body is null ? null : CurlContent.FromBytes(body),
};

// A put, answered with its status.
async Task<(int Status, byte[] Body, string Type)> Put(string url, byte[] body, CurlAuthenticationOptions login)
{
    CurlResponse response = await client.SendAsync(Build(HttpMethod.Put, url, login, body), CancellationToken.None);
    await using Stream stream = response.Body;
    return (response.StatusCode, [], "");
}

// A get: the status, the bytes and the Content-Type header.
async Task<(int Status, byte[] Body, string Type)> Get(string url, CurlAuthenticationOptions login)
{
    CurlResponse response = await client.SendAsync(Build(HttpMethod.Get, url, login), CancellationToken.None);
    await using Stream stream = response.Body;
    using var bytes = new MemoryStream();
    await stream.CopyToAsync(bytes);
    string type = response.Headers.FirstOrDefault(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? "";
    return (response.StatusCode, bytes.ToArray(), type);
}

// One line for a call: its status and text, or, when a gs:// refusal throws, the status and error code from
// the exception's message.
async Task<string> Show(string label, Func<Task<(int Status, byte[] Body, string Type)>> call)
{
    try
    {
        (int status, byte[] body, _) = await call();
        string text = Encoding.UTF8.GetString(body).TrimEnd();
        return $"{label}: {status}{(text.Length > 0 ? " " + text : "")}";
    }
    catch (CurlException failure)
    {
        Match named = Regex.Match(failure.Message, @"returned error (\d+): (\w+)");
        return $"{label}: {failure.Code} {named.Groups[1]} {named.Groups[2]}";
    }
}
