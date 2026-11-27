// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An MCP server over TLS that lists its tools only to a client with its token, in one .NET program.
// Run: dotnet run require-a-token.cs -- <case>
//   token        mcps, the certificate trusted, the token sent (the default)
//   call         the same, with a tools/call as the request's Body
//   call-none    the tools/call with no token
//   none         no token          wrong       another token
//   password     a user name and password instead of a token
//   untrusted    the token, but the certificate not trusted
//   plain        the token over plain mcp
//   plaintext    the same, with AllowPlaintextAuthentication
//   anonymous    mcps with AllowAnonymous, no token
//   no-cert      mcps with no certificate
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "token";
bool plain = which is "plain" or "plaintext";

// The server: one token (an account with an empty user name) and one password account.
var options = new SurlServerOptions
{
    ListenUrls = { plain ? "mcp://127.0.0.1:0" : "mcps://127.0.0.1:0" },
    AllowPlaintextAuthentication = which == "plaintext",
    AllowAnonymous = which == "anonymous",
};
if (!plain && which != "no-cert")
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

options.Accounts.Add(new SurlAccount("", "agent-token-1"));
options.Accounts.Add(new SurlAccount("alice", "secret"));
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"start refused: {e.ExitCode} ({(int)e.ExitCode})");
    Console.WriteLine(e.Message);
    return (int)e.ExitCode;
}

await using (server)
{
    // The client: trusts server.pem as its own authority, and logs in as the case says.
    ReadOnlyMemory<byte>? authority = File.ReadAllBytes("server.pem");
    var login = which switch
    {
        "none" or "call-none" or "anonymous" => null,
        "wrong" => new CurlAuthenticationOptions { XOAuth2Bearer = "agent-token-2" },
        "password" => new CurlAuthenticationOptions { UserName = "alice", Password = "secret" },
        _ => new CurlAuthenticationOptions { XOAuth2Bearer = "agent-token-1" },
    };
    await using CurlClient client = CurlClient.Create(new CurlClientOptions
    {
        Authentication = login,
        Tls = which == "untrusted" ? null : new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = authority } },
    });

    try
    {
        // A request to /mcp with no Body lists the tools, one line each; a Body is sent as a tools/call.
        CurlRequest request = which is "call" or "call-none"
            ? new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}mcp") { Body = "{\"name\":\"read_file\",\"arguments\":{\"path\":\"hello.txt\"}}"u8.ToArray() }
            : new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}mcp");
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        Console.Write(await new StreamReader(body).ReadToEndAsync());
        return 0;
    }
    catch (CurlException e)
    {
        Console.WriteLine($"refused: {e.Code} ({(int)e.Code}): {e.Message}");
        return (int)e.Code;
    }
}
