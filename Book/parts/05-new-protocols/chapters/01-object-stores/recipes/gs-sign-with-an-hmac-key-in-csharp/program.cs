// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Logs in to Cloud Storage with an HMAC key and with an OAuth token, refuses the wrong ones, and shows that
// writes need AllowUploads, with Curl.Api against two Surl.Api servers in one process. Run: dotnet run program.cs
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

// Two servers on the same object, in memory. Both hold two accounts: an HMAC key pair, and an empty name
// whose password is an OAuth token. writable also allows uploads. server.pem and server.key are in the folder.
Task<SurlServer> Start(bool uploads)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "gs://127.0.0.1:0" },
        CertificateFile = "server.pem",
        KeyFile = "server.key",
        AllowUploads = uploads,
    };
    options.Accounts.Add(new SurlAccount("GOOGTESTKEY", "s3cret"));
    options.Accounts.Add(new SurlAccount("", "ya29.t0ken"));
    options.Files["photos/hello.txt"] = "hello from surl\n"u8.ToArray();
    return SurlServer.StartAsync(options);
}

await using SurlServer main = await Start(uploads: false);
await using SurlServer writable = await Start(uploads: true);

await using CurlClient client = CurlClient.Create();
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };
CurlAuthenticationOptions Hmac(string key, string secret) => new() { UserName = key, Password = secret };
// Over a gs:// URL the token alone is sent as Bearer; over https:// the method must be named as well.
CurlAuthenticationOptions Token(string token) => new()
{
    XOAuth2Bearer = token,
    HttpAuthentication = HttpAuthenticationMethods.Bearer,
};

await Show("hmac", main, "gs://photos/hello.txt", Hmac("GOOGTESTKEY", "s3cret"));
await Show("bearer", main, "gs://photos/hello.txt", Token("ya29.t0ken"));
await Show("wrong secret", main, "gs://photos/hello.txt", Hmac("GOOGTESTKEY", "wrong"));
await Show("unknown key", main, "gs://photos/hello.txt", Hmac("GOOGOTHERKEY", "s3cret"));
await Show("unknown token", main, "gs://photos/hello.txt", Token("ya29.other"));
await Show("no login", main, "gs://photos/hello.txt", new CurlAuthenticationOptions());
// A put goes over https:// to Google's host, as any HTTP client sends it, with the token. Over https:// a
// refusal is an ordinary response, not an exception.
const string Object = "https://storage.googleapis.com/photos/";
await Show("uploads off bearer", main, Object + "note.txt", Token("ya29.t0ken"), "a note");
await Show("uploads on bearer", writable, Object + "note.txt", Token("ya29.t0ken"), "a note");
await Show("uploads on unknown token", writable, Object + "other.txt", Token("ya29.other"), "a note");

// The accepted put is an object in the writable server's store; the refused ones left nothing.
foreach ((string label, SurlServer server, string name) in new[] { ("writable", writable, "note.txt"), ("main", main, "note.txt"), ("writable", writable, "other.txt") })
{
    try
    {
        SurlFileContents stored = await server.Files.ReadAsync($"/photos/{name}");
        Console.WriteLine($"stored {label} {name}: {System.Text.Encoding.UTF8.GetString(stored.Bytes.Span).TrimEnd()}");
    }
    catch (SurlNotFoundException)
    {
        Console.WriteLine($"stored {label} {name}: nothing");
    }
}
return 0;

// One request, a get, or a put when there is a body. An answer prints its status and body, or the error code
// in an error body; a gs:// refusal throws CurlException, named from the status and error code in the message.
async Task Show(string label, SurlServer server, string url, CurlAuthenticationOptions login, string? upload = null)
{
    var request = new CurlRequest(upload is null ? HttpMethod.Get : HttpMethod.Put, url)
    {
        Network = new CurlNetworkOptions { ConnectTo = [$"::127.0.0.1:{server.BoundUrl.Port}"] },
        Tls = trust,
        Authentication = login,
        Body = upload is null ? null : System.Text.Encoding.UTF8.GetBytes(upload),
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = (await new StreamReader(body).ReadToEndAsync()).TrimEnd();
        Match code = Regex.Match(text, @"<Code>(\w+)</Code>");
        Console.WriteLine(code.Success ? $"{label}: {response.StatusCode} {code.Groups[1]}" : $"{label}: {response.StatusCode} {text}");
    }
    catch (CurlException failure)
    {
        Match named = Regex.Match(failure.Message, @"returned error (\d+): (\w+)");
        Console.WriteLine($"{label}: {failure.Code} {named.Groups[1]} {named.Groups[2]}");
    }
}
