// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Binds to an LDAP directory with a password over LDAPS, and over plain LDAP, with Surl.Api and Curl.Api,
// in one process. Run: dotnet run program.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// One server, an ldaps and an ldap listener on one directory and one account, nothing loosened.
var options = new SurlServerOptions
{
    ListenUrls = { "ldaps://127.0.0.1:0", "ldap://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("alice", "s3cret") },
};
await using SurlServer server = await SurlServer.StartAsync(options);
await server.Directory.AddEntriesAsync("""
    dn: dc=example,dc=com
    objectClass: domain
    dc: example

    dn: cn=bob,dc=example,dc=com
    objectClass: person
    cn: bob
    mail: bob@example.com
    """);
string ldaps = server.BoundUrls[0].ToString(), ldap = server.BoundUrls[1].ToString();
const string Search = "dc=example,dc=com?mail?sub?(cn=bob)";

await using CurlClient client = CurlClient.Create();
var skipCheck = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false, VerifyHost = false } };
var trustIt = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};

await Bind("ldaps, right password", ldaps + Search, "s3cret", skipCheck);
await Bind("ldaps, wrong password", ldaps + Search, "guess", skipCheck);
await Bind("ldap, right password", ldap + Search, "s3cret", tls: null);
await Bind("ldap, wrong password", ldap + Search, "guess", tls: null);
await Bind("ldaps, certificate checked", ldaps + Search, "s3cret", tls: null);
await Bind("ldaps, certificate trusted", ldaps + Search, "s3cret", trustIt);
return 0;

// One search with a simple bind as alice; prints the entry's attribute lines, or the curl error code thrown.
async Task Bind(string label, string url, string password, CurlTlsOptions? tls)
{
    var request = new CurlRequest(HttpMethod.Get, url)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = password },
    };
    if (tls is not null)
    {
        request = request with { Tls = tls };
    }

    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        Console.WriteLine($"{label}: {string.Join(", ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()))}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
    }
}
