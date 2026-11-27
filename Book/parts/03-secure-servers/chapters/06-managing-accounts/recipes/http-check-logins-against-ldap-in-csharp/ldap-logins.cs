// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test whose web server holds no passwords for the people: Surl.Api hosts a directory and a web
// server that checks every login it does not hold against it, and the test manages the people in the
// directory alone while both run. server.pem and server.key are in the folder.
// Run: dotnet run ldap-logins.cs [-- <case>]
//   (none)    alice, whom only the directory holds, logs in (the default)
//   starter   carol: not in the directory, then an entry only, then an entry and an account
//   leaver    alice logs in, her directory account is removed, she logs in again
//   events    the starter's logins as the web server judged them
//   shadow    bob, whom the web server also holds with a password of its own
//   no-login  a request with no login
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "run";

// The directory: an LDAP server, its people seeded as entries. Its accounts check binds: alice, bob,
// and svc, the web server's service account. The certificate lets it upgrade with StartTLS.
await using SurlServer directory = await SurlServer.StartAsync(new SurlServerOptions
{
    ListenUrls = { "ldap://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("alice", "wonderland"), new SurlAccount("bob", "builder"), new SurlAccount("svc", "svcpass") },
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

// The web server: the report over HTTPS, every login it holds no account for checked against the
// directory, searched as svc. Its own accounts: ops, so every request needs a login, and an old bob.
File.WriteAllText("svc.pass", "svcpass\n");
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("ops", "opspass"), new SurlAccount("bob", "localpass") },
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
    case "starter" or "events":
        string before = await Get("carol", "marvel");
        await directory.Directory.AddEntriesAsync("""
            dn: uid=carol,ou=people,dc=example,dc=com
            objectClass: inetOrgPerson
            uid: carol
            cn: Carol Danvers
            sn: Danvers
            """);
        string entryOnly = await Get("carol", "marvel");
        directory.Accounts.Add("carol", "marvel");
        string both = await Get("carol", "marvel");
        if (which == "starter")
        {
            Console.WriteLine($"carol, not in the directory: {before}");
            Console.WriteLine($"carol, entry only: {entryOnly}");
            Console.WriteLine($"carol, entry and account: {both}");
        }
        else
        {
            foreach (LoginJudgedEvent e in web.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged, Account = "carol" }).Cast<LoginJudgedEvent>())
            {
                Console.WriteLine($"web judged: {e.Scheme} {e.Mechanism} {e.Account} {(e.IsAccepted ? "accepted" : "refused")}");
            }
        }

        break;
    case "leaver":
        Console.WriteLine($"alice: {await Get("alice", "wonderland")}");
        directory.Accounts.Remove("alice");
        Console.WriteLine($"alice, account removed from the directory: {await Get("alice", "wonderland")}");
        break;
    case "shadow":
        Console.WriteLine($"bob, directory password: {await Get("bob", "builder")}");
        Console.WriteLine($"bob, the web server's own password: {await Get("bob", "localpass")}");
        break;
    case "no-login":
        Console.WriteLine($"no login: {await Get(null, null)}");
        break;
    default:
        Console.WriteLine($"alice: {await Get("alice", "wonderland")}");
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
