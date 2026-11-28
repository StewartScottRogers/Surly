// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Cancels a request to a server that stalls, then checks the next request on the same client works.
// Run: dotnet run cancel.cs -- <case>
//   cancel     cancel the stuck request after one second with a CancellationToken (the default)
//   timeout    let the client's own Timeout of one second end it instead
//   one-slot   cancel as above, against a server that takes one connection per address
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Diagnostics;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "cancel";

// The server: /stuck.txt is held for a minute before any reply; /hello.txt is answered at once.
// In one-slot, the server holds one connection per address at most. It records what each connection carried.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    MaxConnectionsPerAddress = which == "one-slot" ? 1 : null,
    RecordExchanges = true,
};
options.Files["stuck.txt"] = "too late\n"u8.ToArray();
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(60)) with { Stage = FaultStage.Command, Path = "/stuck.txt" });
await using var server = await SurlServer.StartAsync(options);
var clientOptions = which == "timeout" ? new CurlClientOptions { Timeout = TimeSpan.FromSeconds(1) } : new CurlClientOptions();
await using CurlClient client = CurlClient.Create(clientOptions);

// The first request in a process sets the engine up, which takes a few seconds: get it out of the way.
CurlResponse warm = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
await using (warm.Body) { }

// The stuck request, given up on after one second.
using var giveUp = new CancellationTokenSource();
if (which != "timeout")
{
    giveUp.CancelAfter(TimeSpan.FromSeconds(1));
}

var clock = Stopwatch.StartNew();
try
{
    CurlResponse stuck = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}stuck.txt"), giveUp.Token);
    await using (stuck.Body) { }
    Console.WriteLine($"stuck.txt answered {stuck.StatusCode}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("stuck.txt: OperationCanceledException");
}
catch (CurlException e)
{
    Console.WriteLine($"stuck.txt: CurlException {e.Code}");
}

Console.WriteLine($"given up after: {(clock.Elapsed.TotalSeconds is >= 1 and < 5 ? "1 to 5 s" : $"{clock.Elapsed.TotalSeconds:F1} s")}");

// The next request on the same client.
clock.Restart();
CurlResponse next = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
await using (Stream body = next.Body)
{
    string text = next.StatusCode == 200 ? (await new StreamReader(body).ReadToEndAsync()).TrimEnd() : "";
    Console.WriteLine($"next: {next.StatusCode} {next.ReasonPhrase} {text}".TrimEnd());
}

Console.WriteLine($"next on a new connection: {(next.Info.NumConnects == 1 ? "yes" : "no")}");

// What the server saw: did any connection carry /hello.txt after /stuck.txt?
bool shared = server.Exchanges.Any(x =>
{
    string received = Encoding.ASCII.GetString(x.Received);
    int stuckAt = received.IndexOf("GET /stuck.txt ");
    return stuckAt >= 0 && received.IndexOf("GET /hello.txt ", stuckAt) >= 0;
});
Console.WriteLine($"next on the given-up connection: {(shared ? "yes" : "no")}");
Console.WriteLine($"next took under 5 s: {(clock.Elapsed.TotalSeconds < 5 ? "yes" : "no")}");
// In one-slot, a 503 or a 200 are both the server's honest answer (see the recipe), so neither fails the run.
return next.StatusCode == 200 || which == "one-slot" ? 0 : 1;
