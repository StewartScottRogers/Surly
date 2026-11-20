// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves one directory over SMB and HTTP with Surl.Api, uploads to it over HTTP PUT with Curl.Api,
// and reads the file back over SMB. Curl.Api 0.2.0 cannot upload over SMB itself.
// Run: dotnet run program.cs -- allow    (also: closed, small)
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

// allow: uploads on. closed: the default, read-only. small: uploads on, but only 10 bytes of them.
string mode = args[0];

// The served directory: each folder in it is an SMB share, so docs is one.
string root = Path.Combine(Path.GetTempPath(), "smb-upload-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "docs"));
string report = Path.Combine(root, "docs", "report.txt");

// One server, two listeners on one directory. NTLMv1 is the login curl makes in an SMB session
// setup (--auth ntlmv1); Digest is the HTTP login.
var options = new SurlServerOptions
{
    ListenUrls = { "smb://127.0.0.1:0", "http://127.0.0.1:0" },
    DataDirectory = root,
    Accounts = { new SurlAccount("alice", "s3cret") },
    AuthenticationMethods = ["ntlmv1", "digest"],
    AllowUploads = mode != "closed",
    MaxFileSize = mode == "small" ? 10 : null,
};
await using var server = await SurlServer.StartAsync(options);
string smb = server.BoundUrls[0] + "docs/report.txt";
string http = server.BoundUrls[1] + "docs/report.txt";

await using CurlClient client = CurlClient.Create();
var smbLogin = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" };
var httpLogin = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret", HttpAuthentication = HttpAuthenticationMethods.Digest };

await Send("upload", http, HttpMethod.Put, "quarterly numbers\n", httpLogin);
Console.WriteLine($"on disk: {(File.Exists(report) ? File.ReadAllText(report).Trim() : "no file")}");
Console.WriteLine($"read over SMB: {await Send(null, smb, HttpMethod.Get, null, smbLogin)}");
if (mode == "allow")
{
    await Send("upload again", http, HttpMethod.Put, "revised numbers\n", httpLogin);
    Console.WriteLine($"read over SMB: {await Send(null, smb, HttpMethod.Get, null, smbLogin)}");
}
return 0;

// One request. A label prints the status; either way the body, or the CurlException's code, comes back.
async Task<string> Send(string? label, string url, HttpMethod method, string? body, CurlAuthenticationOptions login)
{
    var request = new CurlRequest(method, url) { Authentication = login };
    if (body is not null) request = request with { Body = System.Text.Encoding.UTF8.GetBytes(body) };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream stream = response.Body;
        string text = (await new StreamReader(stream).ReadToEndAsync()).Trim();
        if (label is not null) Console.WriteLine($"{label}: {(int)response.StatusCode}");
        return text;
    }
    catch (CurlException failure)
    {
        return $"CurlException {(int)failure.Code} {failure.Code}";
    }
}
