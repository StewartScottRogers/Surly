// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run chain.cs -- SCENARIO`. The certificates come from setup: in
// recipe.yml, which makes root.pem, a leaf chain.pem and others, in the program's folder.
//   fetch (the default), leaf-only, no-trust, root-in-the-file, leaf-first, key-match
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Tls;
using Surl.Api;
using Surl.Protocol.Abstractions;

string scenario = args.Length > 0 ? args[0] : "fetch";

// Which certificate file the server sends, and the key for the leaf at its head.
(string cert, string key) = scenario switch
{
    "leaf-only" => ("leaf2.pem", "leaf2.key"),
    "root-in-the-file" => ("full.pem", "leaf.key"),
    "leaf-first" => ("reversed.pem", "leaf.key"),
    "key-match" => ("chain.pem", "inter.key"),
    _ => ("chain.pem", "leaf.key"),
};
var options = new SurlServerOptions { ListenUrls = { "https://127.0.0.1:0" }, CertificateFile = cert, KeyFile = key };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    // A certificate file the server cannot use stops the start, as it stops surl: exit 58.
    Console.WriteLine($"start refused: {e.ExitCode} ({(int)e.ExitCode})");
    Console.WriteLine(e.Message.StartsWith("(58) Could not load the server certificate: "));
    return 0;
}

await using (server)
{
    // The client trusts the root alone, or nothing. NoRevoke is curl's --ssl-no-revoke: the test
    // certificates carry no revocation address, which Windows' Schannel would otherwise insist on.
    ReadOnlyMemory<byte>? root = scenario == "no-trust" ? null : File.ReadAllBytes("root.pem");
    var peer = new CurlTlsPeerOptions { SslOptions = SslOptions.NoRevoke, CertificateAuthorityBlob = root };
    await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = peer } });
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
        await using Stream body = response.Body;
        Console.WriteLine((int)response.StatusCode);
        Console.Write(await new StreamReader(body).ReadToEndAsync());
    }
    catch (CurlException e)
    {
        Console.WriteLine($"verification failed: {e.Code} ({(int)e.Code})");
    }
}
return 0;
