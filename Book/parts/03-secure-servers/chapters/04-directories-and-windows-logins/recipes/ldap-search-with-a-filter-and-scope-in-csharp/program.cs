// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Searches an LDAP directory by base, scope, filter and attributes with Surl.Api and Curl.Api, in one process.
// Run: dotnet run program.cs
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// The directory: two branches under dc=example,dc=com, seeded as LDIF text into a running server.
const string Ldif = """
    dn: dc=example,dc=com
    objectClass: domain
    dc: example

    dn: ou=people,dc=example,dc=com
    objectClass: organizationalUnit
    ou: people

    dn: cn=alice,ou=people,dc=example,dc=com
    objectClass: person
    cn: alice
    sn: Liddell
    mail: alice@example.com

    dn: cn=bob,ou=people,dc=example,dc=com
    objectClass: person
    cn: bob
    sn: Builder
    mail: bob@example.com

    dn: ou=groups,dc=example,dc=com
    objectClass: organizationalUnit
    ou: groups

    dn: cn=staff,ou=groups,dc=example,dc=com
    objectClass: groupOfNames
    cn: staff
    member: cn=alice,ou=people,dc=example,dc=com
    """;

var options = new SurlServerOptions { ListenUrls = { "ldap://127.0.0.1:0" }, AllowAnonymous = true };
await using SurlServer server = await SurlServer.StartAsync(options);
await server.Directory.AddEntriesAsync(Ldif);
await using CurlClient client = CurlClient.Create();

await Search("one, (sn=B*)", "ou=people,dc=example,dc=com?mail?one?(sn=B*)");
await Search("base", "ou=people,dc=example,dc=com?ou?base?(objectClass=*)");
await Search("one from the top", "dc=example,dc=com?ou?one?(objectClass=organizationalUnit)");
await Search("sub, (cn=staff)", "dc=example,dc=com?cn?sub?(cn=staff)");
await Search("one, (cn=staff)", "dc=example,dc=com?cn?one?(cn=staff)");
await Search("and", "dc=example,dc=com?cn?sub?(&(objectClass=person)(mail=*example.com))");
await Search("not", "ou=people,dc=example,dc=com?cn?one?(!(cn=bob))");
await Search("no match", "ou=people,dc=example,dc=com?cn?one?(cn=zed)");
await Search("missing base", "ou=nowhere,dc=example,dc=com?cn?one?(cn=alice)");

// Without AllowAnonymous the same anonymous search is refused.
await using SurlServer strict = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "ldap://127.0.0.1:0" } });
await Search("strict", "dc=example,dc=com?cn?sub?(cn=alice)", strict);
return 0;

// One search as an anonymous client (an empty name and password, curl's -u :). The entries print as one line,
// each DN with its attributes, so the layout of the LDAP library underneath does not matter.
async Task Search(string label, string search, SurlServer? on = null)
{
    var request = new CurlRequest(HttpMethod.Get, $"{(on ?? server).BoundUrl}{search}")
    {
        Authentication = new CurlAuthenticationOptions { UserPassword = ":" },
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        string[] entries = text.Replace("\r", "").Split("DN: ", StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"{label}: {entries.Length} [{string.Join(" | ", entries.Select(e => string.Join(", ", e.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()))))}]");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}");
    }
}
