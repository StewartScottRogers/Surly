// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test class's fixture: one in-process surl, the class's zone seeded once and snapshotted, and the
// snapshot restored before each test, so a test that adds a zone leaves nothing behind for the next.
// Run: dotnet run zone-fixture.cs [-- case]
//   (no argument)  seed, snapshot, a test adds a zone, restore, the next test finds the baseline
//   no-restore     the same, with no restore before the next test
//   reset          ResetAsync in place of RestoreAsync
//   twice          two tests add zones, each followed by a restore of the one snapshot
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "restore";

// The class fixture: one DNS server, the class's zone, and a snapshot of it.
await using var server = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "dns://127.0.0.1:0" } });
await using CurlClient client = CurlClient.Create();
await server.Zones.AddZoneAsync("example.test", """
    $ORIGIN example.test.
    $TTL 300
    @   IN SOA ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300
    @   IN NS  ns1.example.test.
    api IN A   192.0.2.10
    www IN A   192.0.2.1
    """);
SurlSnapshot baseline = await server.SnapshotAsync();
string expected = await Show();
Console.WriteLine($"baseline: {expected}");

// A test that needs more names adds a zone of its own under the class's domain.
await AddBeta("192.0.2.30");
Console.WriteLine($"test 1: {await Show()}");

switch (which)
{
    case "no-restore":
        Console.WriteLine($"test 2 finds the baseline: {await Show() == expected}");
        return 1;
    case "reset":
        await server.ResetAsync();
        Console.WriteLine($"after ResetAsync: {await Show()}");
        return 1;
    case "twice":
        await server.RestoreAsync(baseline);
        Console.WriteLine($"test 2 finds the baseline: {await Show() == expected}");
        await AddBeta("192.0.2.31");
        await server.RestoreAsync(baseline);
        Console.WriteLine($"test 3 finds the baseline: {await Show() == expected}");
        return 0;
    default:
        await server.RestoreAsync(baseline);
        Console.WriteLine($"after RestoreAsync: {await Show()}");
        Console.WriteLine($"test 2 finds the baseline: {await Show() == expected}");
        return 0;
}

Task AddBeta(string address) => server.Zones.AddZoneAsync("beta.example.test", $"""
    $ORIGIN beta.example.test.
    $TTL 60
    @   IN SOA ns1.example.test. hostmaster.example.test. 1 3600 600 86400 60
    @   IN NS  ns1.example.test.
    api IN A   {address}
    """);

// What a test sees: the zones held and two names, one from the class's zone and one a test adds.
async Task<string> Show() =>
    $"zones [{string.Join(" ", await server.Zones.ListAsync())}], api.example.test {await Lookup("api.example.test")}, "
    + $"api.beta.example.test {await Lookup("api.beta.example.test")}";

// One A lookup over UDP: the status from the header line and the address from the answer line, if any.
async Task<string> Lookup(string name)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{name}?type=A"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    string status = Regex.Match(text, @"status: (\w+)").Groups[1].Value;
    Match address = Regex.Match(text, @"\tA\t(\S+)");
    return address.Success ? $"{status} {address.Groups[1].Value}" : status;
}
