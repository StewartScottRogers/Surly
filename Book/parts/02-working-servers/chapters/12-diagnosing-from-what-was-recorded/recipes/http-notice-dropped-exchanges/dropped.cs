// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A long test asks the server's record whether the app opened its session, after 1,202 requests.
// The record keeps at most MaxRecordedExchanges; the test reads DroppedExchangeCount so that a
// request the record no longer holds is not reported as a request never made.
// Run: dotnet run dropped.cs [-- <case>]
//   (no argument)  room for 100 exchanges; the app opens its session first
//   oldest         the same, then the oldest and newest requests the record still holds
//   roomy          room for 2,000 exchanges
//   no-session     room for 2,000; the app never opens its session
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "small";
int room = mode is "roomy" or "no-session" ? 2000 : 100;

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true, MaxRecordedExchanges = room };
options.Files["session/start"] = "ok\n"u8.ToArray();
options.Files["session/end"] = "ok\n"u8.ToArray();
for (int i = 1; i <= 1200; i++)
{
    options.Files[$"items/{i}"] = Encoding.ASCII.GetBytes($"item {i}\n");
}

await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient app0 = CurlClient.Create();

// The app: open the session, read 1,200 items with eight workers side by side, close the session.
// Each request closes its connection (Connection: close), so each is an exchange of its own.
int requests = 0;
if (mode != "no-session")
{
    await Get(app0, "session/start");
}

var items = new System.Collections.Concurrent.ConcurrentQueue<int>(Enumerable.Range(1, 1200));
await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
{
    await using CurlClient worker = CurlClient.Create();
    while (items.TryDequeue(out int i))
    {
        await Get(worker, $"items/{i}");
    }
}));
await Get(app0, "session/end");

// The record is written off the serving path; give it at most 10 seconds to catch up.
for (int tries = 0; tries < 100 && server.Exchanges.Count + server.DroppedExchangeCount < requests; tries++)
{
    await Task.Delay(100);
}

int kept = server.Exchanges.Count;
long dropped = server.DroppedExchangeCount;
Console.WriteLine($"requests made: {requests}; exchanges kept: {kept}; dropped: {dropped}; kept + dropped: {kept + dropped}");

if (mode == "oldest")
{
    Console.WriteLine($"oldest kept: {RequestLine(server.Exchanges[0])}");
    Console.WriteLine($"newest kept: {RequestLine(server.Exchanges[^1])}");
    return 0;
}

// The test's question: did the app open its session?
bool found = server.Exchanges.Any(e => RequestLine(e) == "GET /session/start");
Console.WriteLine($"GET /session/start in the record: {(found ? "yes" : "no")}");
if (found)
{
    Console.WriteLine("PASS: the app opened its session");
    return 0;
}

if (dropped > 0)
{
    Console.WriteLine($"INCONCLUSIVE: {dropped} exchanges were dropped, oldest first; the request may be among them");
    return 2;
}

Console.WriteLine("FAIL: nothing was dropped, so the app never opened its session");
return 1;

// One GET that closes its connection.
async Task Get(CurlClient client, string path)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}") { Headers = [new("Connection", "close")] }, CancellationToken.None);
    await response.Body.DisposeAsync();
    Interlocked.Increment(ref requests);
}

// The request line of an exchange: the received bytes up to the first line end, without " HTTP/1.1".
static string RequestLine(SurlExchangeRecord exchange)
{
    string head = Encoding.ASCII.GetString(exchange.Received);
    int end = head.IndexOf("\r\n", StringComparison.Ordinal);
    string line = end < 0 ? head : head[..end];
    return line.EndsWith(" HTTP/1.1") ? line[..^9] : line;
}
