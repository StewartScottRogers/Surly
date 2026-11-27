// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves an SMBS share with Surl.Api and reads a file from it with Curl.Api.
// Run: dotnet run program.cs -- trusted    (also: untrusted, wrong-password, no-certificate, self-signed, self-signed-skip)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args[0];

// One account, NTLMv1 (the login curl makes in an SMB session setup), one file in the share docs.
// The certificate and key are the files openssl wrote (--cert, --key), unless the server makes its own (--self-signed).
bool selfSigned = mode.StartsWith("self-signed");
var options = new SurlServerOptions
{
    ListenUrls = { "smbs://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    AuthenticationMethods = ["ntlmv1"],
    SelfSigned = selfSigned,
    CertificateFile = selfSigned || mode == "no-certificate" ? null : "server.pem",
    KeyFile = selfSigned || mode == "no-certificate" ? null : "server.key",
};
options.Files["docs/readme.txt"] = "read me first\n"u8.ToArray();
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"server refused to start: {e.Message}");
    return 58;
}
await using var _ = server;

// The client: trusts server.pem (--cacert), or nothing (the default), or skips the check (-k).
var peer = mode switch
{
    "trusted" or "wrong-password" => new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" },
    "self-signed-skip" => new CurlTlsPeerOptions { VerifyPeer = false },
    _ => new CurlTlsPeerOptions(),
};
var login = new CurlAuthenticationOptions { UserName = "alice", Password = mode == "wrong-password" ? "guess" : "s3cret" };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = peer } });
try
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}docs/readme.txt") { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine($"read: {(await new StreamReader(body).ReadToEndAsync()).Trim()}");
}
catch (CurlException failure)
{
    Console.WriteLine($"read: CurlException {(int)failure.Code} {failure.Code}");
}
return 0;
