// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// One CurlClient with its user agent and timeouts set once, and a test that reads, from what
// Surl.Api recorded, the User-Agent each request arrived with.
// Run: dotnet run client-defaults.cs [-- override|bare|timeout]
//   (no argument) three requests through the configured client
//   override      the second request sets its own user agent
//   bare          the same three requests through a client with no options
//   timeout       a request to a path the server holds for 5 seconds
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Diagnostics;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "defaults";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["slow.txt"] = "late\n"u8.ToArray();
options.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(5)) with { Stage = FaultStage.Command, Path = "/slow.txt" });
await using var server = await SurlServer.StartAsync(options);

// The defaults, set once: every request this client makes carries them.
var defaults = new CurlClientOptions
{
    UserAgent = "report-tests/1.0",
    Timeout = TimeSpan.FromSeconds(2),
    ConnectTimeout = TimeSpan.FromSeconds(5),
};
await using CurlClient client = mode == "bare" ? CurlClient.Create() : CurlClient.Create(defaults);

if (mode == "timeout")
{
    var clock = Stopwatch.StartNew();
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}slow.txt"), CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        Console.WriteLine($"answered: {response.StatusCode}");
    }
    catch (CurlException e)
    {
        Console.WriteLine($"failed: {e.Code}");
    }

    Console.WriteLine($"gave up before the server answered: {(clock.Elapsed < TimeSpan.FromSeconds(5) ? "yes" : "no")}");
    return 0;
}

for (int n = 1; n <= 3; n++)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt");
    if (mode == "override" && n == 2)
    {
        // One request may set its own; the client's default is untouched for the next.
        request = request with { Http = new CurlHttpOptions { UserAgent = "one-off/2.0" } };
    }

    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
}

// The test: the User-Agent line of each request head the server received, in order. The record can
// trail the client by a moment, so read it again until it holds all three heads, for up to 10 s.
string received = string.Concat(server.Exchanges.Select(e => Encoding.ASCII.GetString(e.Received)));
for (var waited = Stopwatch.StartNew(); received.Split("\r\n\r\n").Length - 1 < 3 && waited.Elapsed.TotalSeconds < 10;)
{
    await Task.Delay(100);
    received = string.Concat(server.Exchanges.Select(e => Encoding.ASCII.GetString(e.Received)));
}

int count = 0;
bool passed = true;
foreach (string head in received.Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries))
{
    string? agent = head.Split("\r\n").FirstOrDefault(l => l.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"request {++count}: {agent ?? "(no User-Agent)"}");
    passed &= agent == "User-Agent: report-tests/1.0";
}

Console.WriteLine(passed ? "every request carried the default" : "not every request carried the default");
return passed ? 0 : 1;
