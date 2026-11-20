// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves files with Surl.Api, holding every request under /slow for five seconds, and fetches a
// slow path and a fast one at once with Curl.Api. Run: dotnet run slow-path.cs -- [seconds] [path]
//   seconds   the client's limit on each transfer (2 if not given; 0 for none)
//   path      the second path, fetched beside /slow/report.txt (/hello.txt if not given)
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Diagnostics;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

double limit = args.Length > 0 ? double.Parse(args[0]) : 2;
string other = args.Length > 1 ? args[1] : "/hello.txt";

// The server: three files in memory, and a rule that holds each request under /slow five seconds.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["slow/report.txt"] = "the slow report\n"u8.ToArray();
options.Files["slowness.txt"] = "not slow at all\n"u8.ToArray();
options.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(5)) with { Stage = FaultStage.Command, Path = "/slow" });
await using SurlServer server = await SurlServer.StartAsync(options);

// The client: one limit for every transfer, as curl -m sets it. Both fetches run at once.
var clientOptions = limit > 0 ? new CurlClientOptions { Timeout = TimeSpan.FromSeconds(limit) } : new CurlClientOptions();
await using CurlClient client = CurlClient.Create(clientOptions);
await Task.WhenAll(Fetch("/slow/report.txt"), Fetch(other));
return 0;

// Prints one line as the fetch ends: its status or curl's error code, and whether it took the hold's five seconds.
async Task Fetch(string path)
{
    var clock = Stopwatch.StartNew();
    string outcome;
    try
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path.TrimStart('/')}"), CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        outcome = $"{response.StatusCode} {response.ReasonPhrase}";
    }
    catch (CurlException failure)
    {
        outcome = $"CurlException {(int)failure.Code} ({failure.Code})";
    }

    Console.WriteLine($"{path}: {outcome}, {(clock.Elapsed.TotalSeconds < 5 ? "within" : "after")} 5 s");
}
