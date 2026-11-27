// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds the LDAP directory of a running Surl.Api server from LDIF text, in steps, and reads it back
// two ways: searched over LDAP with Curl.Api, and exported as LDIF. Run: dotnet run program.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// Each test seeds the people it needs. The first batch is the tree's top and alice.
const string First = """
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
    """;

// The second batch adds bob to the running server. His entry holds a password.
const string Second = """
    dn: cn=bob,ou=people,dc=example,dc=com
    objectClass: person
    cn: bob
    sn: Builder
    userPassword: b0bpass
    """;

// A batch is all or none: carol is fine, dave's parent does not exist.
const string Mixed = """
    dn: cn=carol,ou=people,dc=example,dc=com
    objectClass: person
    cn: carol
    sn: Singer

    dn: cn=dave,ou=nowhere,dc=example,dc=com
    objectClass: person
    cn: dave
    sn: Digger
    """;

const string People = "dc=example,dc=com?cn?sub?(objectClass=person)";

var options = new SurlServerOptions { ListenUrls = { "ldap://127.0.0.1:0" }, AllowAnonymous = true };
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

Console.WriteLine($"empty: {await Search(server, People)}");

await server.Directory.AddEntriesAsync(First);
Console.WriteLine($"after the first batch: {await Search(server, People)}");

await server.Directory.AddEntriesAsync(Second);
Console.WriteLine($"after the second batch: {await Search(server, People)}");

Console.WriteLine($"mixed batch: {await Seed(server, Mixed)}");
Console.WriteLine($"carol after it: {await Search(server, "dc=example,dc=com?cn?sub?(cn=carol)")}");
Console.WriteLine($"alice again: {await Seed(server, First.Split("\n\n")[2])}");
Console.WriteLine($"not LDIF: {await Seed(server, "this is not LDIF")}");

string exported = await server.Directory.ExportLdifAsync();
Console.WriteLine($"export: alice {Has(exported, "cn=alice,ou=people")}, bob {Has(exported, "cn=bob,ou=people")}, userPassword {Has(exported, "userPassword")}, b0bpass {Has(exported, "b0bpass")}");

// A second server is its own directory, empty again.
await using SurlServer other = await SurlServer.StartAsync(options);
Console.WriteLine($"another server: {await Search(other, People)}");
return 0;

static string Has(string text, string what) => text.Contains(what) ? "present" : "absent";

// Seeds a batch and says what happened: nothing thrown, or SurlSeedRefusedException.
static async Task<string> Seed(SurlServer server, string ldif)
{
    try
    {
        await server.Directory.AddEntriesAsync(ldif);
        return "added";
    }
    catch (SurlSeedRefusedException)
    {
        return "refused";
    }
}

// One search as an anonymous client (an empty name and password, curl's -u :). The entries print as
// one line, each DN with its attributes, sorted, so the layout of the LDAP library underneath does
// not matter.
async Task<string> Search(SurlServer on, string search)
{
    var request = new CurlRequest(HttpMethod.Get, $"{on.BoundUrl}{search}")
    {
        Authentication = new CurlAuthenticationOptions { UserPassword = ":" },
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        string[] entries = text.Replace("\r", "").Split("DN: ", StringSplitOptions.RemoveEmptyEntries)
            .Select(e => string.Join(", ", e.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())))
            .Order().ToArray();
        return $"{entries.Length} [{string.Join(" | ", entries)}]";
    }
    catch (CurlException failure)
    {
        return $"CurlException {(int)failure.Code} {failure.Code}";
    }
}
