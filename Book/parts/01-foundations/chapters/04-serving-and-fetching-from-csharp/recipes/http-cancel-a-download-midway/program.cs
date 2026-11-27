// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Downloads the head of a 64 MiB file with Curl.Api, stops once it has the first MiB, and shows the
// transfer stopped. Run: dotnet run program.cs [-- <case>]
//   cancel    cancel the body's token once 1 MiB has arrived (the default)
//   dispose   dispose the body stream instead, with no token
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Diagnostics;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "cancel";
const int FileSize = 64 * 1024 * 1024;
const int Wanted = 1024 * 1024;

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["big.bin"] = new byte[FileSize];
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
// Timeout bounds every transfer, its body included, so nothing here can hang past 30 s.
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(30) });

// The first request in a process sets the engine up, which takes a few seconds: get it out of the way.
CurlResponse warm = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
await using (warm.Body) { }

// The download: the token given to SendAsync also aborts the body while it streams.
using var enough = new CancellationTokenSource();
CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}big.bin"), enough.Token);
Console.WriteLine($"big.bin: {response.StatusCode} {response.ReasonPhrase}, Content-Length {response.Headers.First(h => h.Key == "Content-Length").Value}");
Stream body = response.Body;
byte[] buffer = new byte[64 * 1024];
long read = 0;
while (read < Wanted)
{
    int n = await body.ReadAsync(buffer);
    if (n == 0) break;
    read += n;
}

Console.WriteLine($"read before stopping: {(read >= Wanted && read < 2 * Wanted ? "1 MiB" : $"{read} bytes")}");
var clock = Stopwatch.StartNew();
if (which == "cancel")
{
    enough.Cancel();
    try
    {
        while (await body.ReadAsync(buffer) > 0) { }
        Console.WriteLine("the body ran to its end");
    }
    catch (OperationCanceledException e)
    {
        Console.WriteLine($"next read: {e.GetType().Name}");
    }

    await body.DisposeAsync();
}
else
{
    await body.DisposeAsync();
    Console.WriteLine("body disposed");
}

Console.WriteLine($"stopped within 2 s: {(clock.Elapsed.TotalSeconds < 2 ? "yes" : "no")}");

// The server's side: the connection that carried big.bin ends, having sent a small part of the file.
// The event comes once the server has seen the connection close: wait for it, 5 s at most.
ExchangeEndedEvent? ended = null;
for (int tries = 0; tries < 50 && ended is null; tries++)
{
    ended = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).OfType<ExchangeEndedEvent>().FirstOrDefault();
    if (ended is null) await Task.Delay(100);
}

string share = ended is null ? "not ended" : ended.SentByteCount < 16 * 1024 * 1024 ? "under 16 MiB of 64 MiB" : $"{ended.SentByteCount} bytes";
Console.WriteLine($"server's exchange ended, sent: {share}");

// The same client goes on: the next request is answered at once.
CurlResponse next = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
await using (Stream nextBody = next.Body)
{
    Console.WriteLine($"next: {next.StatusCode} {(await new StreamReader(nextBody).ReadToEndAsync()).TrimEnd()}, new connection: {(next.Info.NumConnects == 1 ? "yes" : "no")}");
}

return 0;
