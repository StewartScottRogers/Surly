// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches a file as byte ranges at once with one CurlClient, joins them in order and compares
// the result with the original, then reads from the server's record the ranges it received.
// Run: dotnet run segments.cs [-- <segments> [slow | past-the-end]]
//   <segments>     how many ranges to fetch at once (default 4)
//   slow           the server holds every request 1 s, so the ranges are all in flight together
//   past-the-end   ask for one range that starts after the last byte instead
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Security.Cryptography;
using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

int count = args.Length > 0 ? int.Parse(args[0]) : 4;
string mode = args.Length > 1 ? args[1] : "";

// The server: 1,000,000 bytes that are the same on every run, every exchange recorded.
byte[] original = new byte[1_000_000];
new Random(2026).NextBytes(original);
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["big.bin"] = original;
if (mode == "slow")
{
    options.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(1)) with { Stage = FaultStage.Command });
}

await using var server = await SurlServer.StartAsync(options);
string url = $"{server.BoundUrl}big.bin";
await using CurlClient client = CurlClient.Create();

if (mode == "past-the-end")
{
    CurlResponse refused = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, url) { Headers = [new("Range", "bytes=2000000-2999999")] }, CancellationToken.None);
    await refused.Body.DisposeAsync();
    Console.WriteLine($"{refused.StatusCode} {refused.ReasonPhrase}, Content-Range {Header(refused, "Content-Range")}");
    return 0;
}

// The size, from a HEAD request; this also pays the first request's one-time set-up.
CurlResponse head = await client.SendAsync(new CurlRequest(HttpMethod.Head, url), CancellationToken.None);
await head.Body.DisposeAsync();
long size = long.Parse(Header(head, "Content-Length"));
long length = (size + count - 1) / count;

// One request per range, all started together; each copies its bytes to its place in the result.
byte[] joined = new byte[size];
string[] lines = await Task.WhenAll(Enumerable.Range(0, count).Select(async i =>
{
    long first = i * length, last = Math.Min(first + length, size) - 1;
    var request = new CurlRequest(HttpMethod.Get, url) { Headers = [new("Range", $"bytes={first}-{last}")] };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    var bytes = new MemoryStream();
    await body.CopyToAsync(bytes);
    bytes.ToArray().CopyTo(joined, first);
    return $"{response.StatusCode} {Header(response, "Content-Range")}, {bytes.Length} bytes";
}));
foreach (string line in lines)
{
    Console.WriteLine(line);
}

bool same = SHA256.HashData(joined).SequenceEqual(SHA256.HashData(original));
Console.WriteLine($"joined: {joined.Length} bytes, {(same ? "identical" : "differs")}");

// The server's side: the Range header of every request it received, and how many connections carried them.
// The record can trail the answers by a moment: look again until every range is there, for up to 10 s.
List<List<string>> Ranged() => server.Exchanges
    .Select(exchange => Encoding.ASCII.GetString(exchange.Received).Split("\r\n").Where(l => l.StartsWith("Range: ")).ToList())
    .Where(ranges => ranges.Count > 0)
    .ToList();
var ranged = Ranged();
for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); ranged.Sum(r => r.Count) < count && DateTime.UtcNow < deadline; ranged = Ranged())
{
    await Task.Delay(100);
}
foreach (string range in ranged.SelectMany(r => r).OrderBy(r => long.Parse(r[13..r.IndexOf('-')])))
{
    Console.WriteLine($"server received {range}");
}

Console.WriteLine($"connections that carried a range: {ranged.Count}");
return same ? 0 : 1;

static string Header(CurlResponse response, string name) =>
    response.Headers.First(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
