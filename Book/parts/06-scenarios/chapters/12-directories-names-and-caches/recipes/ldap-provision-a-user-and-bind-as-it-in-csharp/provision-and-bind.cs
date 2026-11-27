// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Tests a provisioning job against an in-process LDAP server: the job adds bob's entry and his
// account, the test finds the entry by search and binds as it, then reads what the server recorded.
// Run: dotnet run provision-and-bind.cs [-- <mode>]
//   (no argument)  the job adds the entry and the account; bob binds with his password
//   guess          the same, but bob binds with a wrong password
//   entry-only     the job adds the entry, userPassword included, but no account
//   strict         a server that keeps the default and refuses a password sent in clear
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "";

const string Tree = """
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
    mail: alice@example.com
    """;

// What the job writes: bob's entry, and his account with the same password.
const string BobDn = "uid=bob,ou=people,dc=example,dc=com";
const string Bob = """
    dn: uid=bob,ou=people,dc=example,dc=com
    objectClass: inetOrgPerson
    uid: bob
    cn: Bob Jones
    sn: Jones
    mail: bob@example.com
    userPassword: b0b-pass
    """;

// The server: tester is the account the test searches as; RecordExchanges keeps the events.
var options = new SurlServerOptions
{
    ListenUrls = { "ldap://127.0.0.1:0" },
    Accounts = { new SurlAccount("tester", "t3st-pass") },
    AllowPlaintextAuthentication = mode != "strict",
    RecordExchanges = true,
};
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
await server.Directory.AddEntriesAsync(Tree);

if (mode == "strict")
{
    Console.WriteLine($"bind as tester: {await Search("ou=people,dc=example,dc=com?cn?sub?(uid=alice)", "tester", "t3st-pass")}");
    return 0;
}

const string FindBob = "ou=people,dc=example,dc=com?cn,mail?sub?(uid=bob)";
Console.WriteLine($"before the job: {await Search(FindBob, "tester", "t3st-pass")}");

// The job. The test stands in for it here; yours runs its own code against the server.
await server.Directory.AddEntriesAsync(Bob);
if (mode != "entry-only")
{
    server.Accounts.Add("bob", "b0b-pass");
}

Console.WriteLine($"search as tester: {await Search(FindBob, "tester", "t3st-pass")}");
string password = mode == "guess" ? "guess" : "b0b-pass";
Console.WriteLine($"bind as bob: {await Search($"{BobDn}?uid?base", BobDn, password)}");

// What the server recorded, each read waiting up to 10 seconds for its event.
var login = (LoginJudgedEvent)await server.WaitForEventAsync(
    new SurlEventQuery { Kind = SurlEventKind.LoginJudged, Account = "bob" }, TimeSpan.FromSeconds(10));
Console.WriteLine($"recorded: {login.Scheme} {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
var seeded = (StoreChangedEvent)await server.WaitForEventAsync(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Directory, Name = BobDn }, TimeSpan.FromSeconds(10));
Console.WriteLine($"recorded: {seeded.Store} {seeded.Change} {seeded.Name}");

string exported = await server.Directory.ExportLdifAsync();
Console.WriteLine($"export: bob {Has(exported, $"dn: {BobDn}")}, userPassword {Has(exported, "userPassword")}, b0b-pass {Has(exported, "b0b-pass")}");
return 0;

static string Has(string text, string what) => text.Contains(what) ? "present" : "absent";

// One search, bound as the given name and password. The entries print as one line, each DN with its
// attributes, so the layout of the LDAP library underneath does not matter; a refusal prints its code.
async Task<string> Search(string search, string user, string secret)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{search}")
    {
        Authentication = new CurlAuthenticationOptions { UserName = user, Password = secret },
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
        return $"refused, CurlException {(int)failure.Code} {failure.Code}";
    }
}
