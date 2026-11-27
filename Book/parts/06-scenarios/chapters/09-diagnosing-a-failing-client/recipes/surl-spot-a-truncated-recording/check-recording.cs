// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that judges a client by the exchanges the server recorded checks first that the record is
// whole: no exchange dropped for room, none cut short. A run of nine fetches, one of them a large file,
// fills a small recording; the check names what is missing and the test refuses to pass on it.
// Run: dotnet run check-recording.cs [-- case]
//   (no argument)  room for 5 exchanges of at most 4 KiB each: the check fails
//   roomy          surl's default room: the check passes
//   events         room for every exchange, but for only 10 events
//   not-recording  RecordExchanges off: there is no record to check
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "small";

// The server records every exchange; how much it keeps is the case's choice.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = which != "not-recording" };
if (which == "small")
{
    options.MaxRecordedExchanges = 5;
    options.MaxRecordedBytesPerExchange = 4096;
}
else if (which == "events")
{
    options.MaxRecordedEvents = 10;
}

options.Files["status.txt"] = "orders api: up\n"u8.ToArray();
options.Files["export.csv"] = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(1, 1000).Select(i => $"{i:D6},order\n")));
await using var server = await SurlServer.StartAsync(options);

// The run: eight status checks and one export, each on its own connection, so each is one exchange.
string[] paths = [.. Enumerable.Repeat("status.txt", 8), "export.csv"];
foreach (string path in paths)
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
}

// Before judging the client, the test checks that the record is whole.
long dropped;
try
{
    dropped = server.DroppedExchangeCount;
}
catch (InvalidOperationException off)
{
    Console.WriteLine($"no record: {off.GetType().Name}");
    return 1;
}

IReadOnlyList<SurlExchangeRecord> kept = server.Exchanges;
List<SurlExchangeRecord> cut = kept.Where(e => e.IsTruncated).ToList();
Console.WriteLine($"{paths.Length} fetches, {kept.Count} exchanges kept, {dropped} dropped, {cut.Count} truncated, {server.DroppedEventCount} events dropped");
foreach (SurlExchangeRecord exchange in cut)
{
    string request = Encoding.ASCII.GetString(exchange.Received).Split("\r\n")[0];
    int bytes = exchange.Received.Length + exchange.Sent.Length;
    Console.WriteLine($"truncated: exchange {exchange.ExchangeId}, {request}, {(bytes <= 4096 ? "at most 4096" : $"{bytes}")} bytes kept");
}

if (dropped > 0 || cut.Count > 0 || server.DroppedEventCount > 0)
{
    Console.WriteLine("FAIL: the recording is partial; raise the limits or record a shorter run");
    return 1;
}

Console.WriteLine("the recording is whole");
return 0;
