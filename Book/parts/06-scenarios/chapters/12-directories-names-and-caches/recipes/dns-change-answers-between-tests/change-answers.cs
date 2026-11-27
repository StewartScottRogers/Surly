// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Three tests share one in-process surl and each wants its own answer for api.example.test: the first
// the old address, the second the new one, the third no zone at all. Between tests the program resets
// the server, which drops the zone, and seeds the next; the client asks with Curl.Api over DNS.
// Run: dotnet run change-answers.cs [-- case]
//   (no argument)  three tests, a reset and a fresh zone between them
//   no-reset       the second test adds its zone without a reset first
//   list           the zones the server holds before and after a reset
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "tests";

// One DNS server for every test, on a free port of the loopback address; no zone yet.
await using var server = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "dns://127.0.0.1:0" } });
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "no-reset":
        await Seed("192.0.2.10");
        Console.WriteLine($"test 1: {await Lookup("api.example.test")}");
        try
        {
            await Seed("192.0.2.20");
        }
        catch (SurlSeedRefusedException refused)
        {
            Console.WriteLine($"test 2 seed refused: {refused.Store} {refused.Refusal}");
            Console.WriteLine($"test 2: {await Lookup("api.example.test")}");
            return 1;
        }

        return 0;
    case "list":
        await Seed("192.0.2.10");
        Console.WriteLine($"zones: {string.Join(" ", await server.Zones.ListAsync())}");
        await server.ResetAsync();
        Console.WriteLine($"after ResetAsync: {(await server.Zones.ListAsync()).Count} zones");
        return 0;
    default:
        // Test 1: the service at its old address.
        await Seed("192.0.2.10");
        Console.WriteLine($"test 1: {await Lookup("api.example.test")}");

        // Test 2: the service moved; the zone is replaced by a reset and a new seed.
        await server.ResetAsync();
        await Seed("192.0.2.20");
        Console.WriteLine($"test 2: {await Lookup("api.example.test")}");

        // Test 3: the name must not resolve at all; a reset alone leaves no zone.
        await server.ResetAsync();
        Console.WriteLine($"test 3: {await Lookup("api.example.test")}");
        return 0;
}

// The zone a test wants: api.example.test at the address it names.
Task Seed(string address) => server.Zones.AddZoneAsync("example.test", $"""
    $ORIGIN example.test.
    $TTL 300
    @   IN SOA ns1.example.test. hostmaster.example.test. 1 3600 600 86400 300
    @   IN NS  ns1.example.test.
    api IN A   {address}
    """);

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
