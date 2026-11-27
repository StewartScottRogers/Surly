// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test changes Redis keys, directory entries and DNS zones in one Surl.Api server; one call puts all three back,
// and the test proves it by asking each store over its own protocol with Curl.Api. Run: dotnet run program.cs [-- case]
//   (no argument)  seed, snapshot, change all three stores, RestoreAsync, read all three again
//   reset          ResetAsync instead: the three stores are empty, and the test seeds them again
//   twice          change and restore, then change and restore again from the same snapshot
//   no-restore     the next test starts with no restore and finds the last test's changes
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "restore";

// Redis, the directory and DNS in one server; the directory answers a client that gives no name.
var options = new SurlServerOptions
{
    ListenUrls = { "redis://127.0.0.1:0", "ldap://127.0.0.1:0", "dns://127.0.0.1:0" },
    AllowAnonymous = true,
};
await using var server = await SurlServer.StartAsync(options);
string redis = server.BoundUrls[0].ToString();
string ldap = server.BoundUrls[1].ToString();
string dns = server.BoundUrls[2].ToString();
await using CurlClient client = CurlClient.Create();

await Seed();
string baseline = await Show();
Console.WriteLine($"baseline: {baseline}");
SurlSnapshot snapshot = await server.SnapshotAsync();

await Change();
Console.WriteLine($"after the test: {await Show()}");

switch (which)
{
    case "reset":
        await server.ResetAsync();
        Console.WriteLine($"after ResetAsync: {await Show()}");
        await Seed();
        Console.WriteLine($"seeded again: {await Show() == baseline}");
        return 0;
    case "twice":
        await server.RestoreAsync(snapshot);
        Console.WriteLine($"first restore is the baseline: {await Show() == baseline}");
        await Change();
        await server.RestoreAsync(snapshot);
        Console.WriteLine($"second restore is the baseline: {await Show() == baseline}");
        return 0;
    case "no-restore":
        Console.WriteLine($"next test finds the baseline: {await Show() == baseline}");
        return 1;
    default:
        await server.RestoreAsync(snapshot);
        Console.WriteLine($"after RestoreAsync: {await Show()}");
        Console.WriteLine($"back at the baseline: {await Show() == baseline}");
        return 0;
}

// What every test starts from: one key, a tree of two entries and one zone.
async Task Seed()
{
    await server.Keys.SetAsync("greeting", "hello", null);
    await server.Directory.AddEntriesAsync("""
        dn: dc=example,dc=com
        objectClass: domain
        dc: example

        dn: cn=alice,dc=example,dc=com
        objectClass: person
        cn: alice
        sn: Liddell
        """);
    await server.Zones.AddZoneAsync("example.test", """
        $ORIGIN example.test.
        $TTL 300
        @   IN SOA ns.example.test. hostmaster.example.test. 1 3600 600 86400 300
        @   IN NS  ns.example.test.
        www IN A   192.0.2.1
        """);
}

// What a test does: replaces the key, adds an entry and a second zone.
async Task Change()
{
    await server.Keys.SetAsync("greeting", "changed", null);
    await server.Directory.AddEntriesAsync("""
        dn: cn=bob,dc=example,dc=com
        objectClass: person
        cn: bob
        sn: Builder
        """);
    await server.Zones.AddZoneAsync("other.test", """
        $ORIGIN other.test.
        $TTL 300
        @   IN SOA ns.other.test. hostmaster.other.test. 1 3600 600 86400 300
        @   IN NS  ns.other.test.
        www IN A   192.0.2.2
        """);
}

// The three stores as a client sees them: the key and the key count, the entries under the root, two names.
async Task<string> Show() =>
    $"greeting {await Redis("GET/greeting")}, keys {await Redis("DBSIZE")}, directory {await Entries()}, "
    + $"www.example.test {await Lookup("www.example.test")}, www.other.test {await Lookup("www.other.test")}";

async Task<string> Redis(string command)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, redis + command), CancellationToken.None);
    await using Stream body = response.Body;
    string text = (await new StreamReader(body).ReadToEndAsync()).TrimEnd('\n');
    return text == "" ? "null" : text;
}

// Entries under the root, an anonymous search (curl's -u :). A tree with no root is curl exit 39.
async Task<string> Entries()
{
    var search = new CurlRequest(HttpMethod.Get, $"{ldap}dc=example,dc=com?cn?sub?(objectClass=*)")
    {
        Authentication = new CurlAuthenticationOptions { UserPassword = ":" },
    };
    try
    {
        CurlResponse response = await client.SendAsync(search, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        return $"{Regex.Matches(text, "DN: ").Count} entries";
    }
    catch (CurlException failure)
    {
        return $"none (curl error {(int)failure.Code})";
    }
}

// One A lookup: the status from the header line and the address from the answer line, if there is one.
async Task<string> Lookup(string name)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{dns}{name}?type=A"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    string status = Regex.Match(text, @"status: (\w+)").Groups[1].Value;
    Match address = Regex.Match(text, @"\tA\t(\S+)");
    return address.Success ? $"{status} {address.Groups[1].Value}" : status;
}
