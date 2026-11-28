// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Hosts gRPC over TLS with Surl.Api and calls it with Curl.Api: trusted, untrusted, the wrong host
// name, the wrong password, the same login over plain grpc, and an empty request.
// Run: dotnet run program.cs [-- case]   (case: trusted, the default, untrusted, wrong-name, wrong-password,
//                                         in-clear, empty, no-certificate)
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text.Json.Nodes;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Tls;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "trusted";

// The server: gRPC over TLS with the certificate setup made, and one account.
// server.pem and server.key are read from the working directory.
var options = new SurlServerOptions
{
    ListenUrls = { which == "in-clear" ? "grpc://127.0.0.1:0" : "grpcs://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
};
if (which != "no-certificate")
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"start refused: {e.ExitCode} ({(int)e.ExitCode})");
    return 1;
}

await using (server)
{
    // The client trusts server.pem as its certificate authority, unless the case is untrusted.
    var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = which == "untrusted" ? null : "server.pem" };
    await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });

    string host = which == "wrong-name" ? "localhost" : "127.0.0.1";
    string url = $"{server.BoundUrl.Scheme}://{host}:{server.BoundUrl.Port}/surl.test.Echo/Echo";
    var request = new CurlRequest(HttpMethod.Get, url)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = which == "wrong-password" ? "wrong" : "s3cret" },
    };
    if (which != "empty")
    {
        // A POST's body is the request message, as JSON; a GET sends the empty message.
        request = request with { Method = HttpMethod.Post, Body = "{\"message\":\"hello\"}"u8.ToArray() };
    }

    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        // The reply is JSON; ToJsonString writes it on one line.
        string reply = JsonNode.Parse(await new StreamReader(body).ReadToEndAsync())!.ToJsonString();
        Console.WriteLine($"{server.BoundUrl.Scheme}: reply {reply}");
        return 0;
    }
    catch (CurlException e)
    {
        Console.WriteLine($"{server.BoundUrl.Scheme}: CurlException {e.Code} ({(int)e.Code})");
        Console.WriteLine(e.Message);
        return (int)e.Code;
    }
}
