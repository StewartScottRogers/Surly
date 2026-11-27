// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of an app whose users live in LDAP: Surl.Api hosts a directory, seeded with the people, and a
// web server that checks the logins it does not hold against that directory; Curl.Api logs in as one of
// the directory's users. server.pem and server.key are in the folder.
// Run: dotnet run program.cs [-- <case>]
//   (none)    alice logs in with her directory password (the default)
//   refused   a wrong password, a name not in the directory, and no login
//   events    what each server recorded about alice's login
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "run";

// The directory: an LDAP server, seeded at run time with the people. Its accounts check binds: alice,
// bob, and svc, the web server's service account. The certificate lets it upgrade with StartTLS.
await using SurlServer directory = await SurlServer.StartAsync(new SurlServerOptions
{
    ListenUrls = { "ldap://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("alice", "wonderland"), new SurlAccount("bob", "builder"), new SurlAccount("svc", "svcpass") },
    RecordExchanges = true,
});
await directory.Directory.AddEntriesAsync("""
    dn: dc=example,dc=com
    objectClass: domain
    dc: example

    dn: ou=people,dc=example,dc=com
    objectClass: organizationalUnit
    ou: people

    dn: uid=alice,ou=people,dc=example,dc=com
    objectClass: inetOrgPerson
    uid: alice
    cn: Alice Liddell
    sn: Liddell

    dn: uid=bob,ou=people,dc=example,dc=com
    objectClass: inetOrgPerson
    uid: bob
    cn: Bob Builder
    sn: Builder
    """);

// The web server: the report over HTTPS, ops its one account of its own, every other login checked
// against the directory, searched as svc, whose password is read from a file.
File.WriteAllText("svc.pass", "svcpass\n");
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("ops", "opspass") },
    AdditionalArguments =
    {
        "--user-ldap",
        $"ldap://127.0.0.1:{directory.BoundUrl.Port}/ou=people,dc=example,dc=com;bind-dn=cn=svc,dc=example,dc=com;bind-password-file=svc.pass;ca-file=server.pem",
    },
    RecordExchanges = true,
};
options.Files["report.txt"] = "q3 total: 1200\n"u8.ToArray();
await using SurlServer web = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(30) });
var trust = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = File.ReadAllBytes("server.pem") } };

switch (which)
{
    case "refused":
        Console.WriteLine($"alice, wrong password: {await Get("alice", "queenofhearts")}");
        Console.WriteLine($"carol: {await Get("carol", "x")}");
        Console.WriteLine($"no login: {await Get(null, null)}");
        break;
    case "events":
        Console.WriteLine($"alice: {await Get("alice", "wonderland")}");
        // The web server judged alice's login; the directory judged two binds on one connection.
        foreach (LoginJudgedEvent e in web.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).OfType<LoginJudgedEvent>())
            Console.WriteLine($"web: {e.Scheme} {e.Account} {(e.IsAccepted ? "accepted" : "refused")}");
        await directory.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "ldap" }, TimeSpan.FromSeconds(10));
        foreach (LoginJudgedEvent e in directory.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).OfType<LoginJudgedEvent>())
            Console.WriteLine($"directory: {e.Scheme} {e.Account} {(e.IsAccepted ? "accepted" : "refused")}");
        break;
    default:
        Console.WriteLine($"alice: {await Get("alice", "wonderland")}");
        Console.WriteLine($"bob: {await Get("bob", "builder")}");
        break;
}

return 0;

// One GET of the report, with HTTP Basic when a name is given: the status, and the body when it is 200.
async Task<string> Get(string? user, string? password)
{
    var request = new CurlRequest(HttpMethod.Get, $"{web.BoundUrl}report.txt")
    {
        Tls = trust,
        Authentication = user is null ? null : new CurlAuthenticationOptions { UserName = user, Password = password },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    return response.StatusCode == 200 ? $"200 {text.TrimEnd()}" : $"{response.StatusCode}";
}
