// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Asks a Surl.Api server for one file in each HTTP version a test may care about, and prints the
// version asked for, the version the response came in, and the request line the server received.
// Run: dotnet run choose-version.cs [-- upgrade]
//   (no argument) HTTP/1.0, HTTP/1.1 and HTTP/2 with prior knowledge
//   upgrade       HTTP/2 without prior knowledge: an HTTP/1.1 request offering to upgrade
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Options.Http;
using Surl.Api;

HttpVersion[] versions = args is ["upgrade"]
    ? [HttpVersion.Http2]
    : [HttpVersion.Http10, HttpVersion.Http11, HttpVersion.Http2PriorKnowledge];

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

foreach (HttpVersion version in versions)
{
    // One client per version, so each request has a connection of its own.
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
    {
        Http = new CurlHttpOptions { HttpVersion = version },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using (Stream body = response.Body)
    {
        await body.CopyToAsync(Stream.Null);
    }

    // What the server received: the first line of the newest exchange, and an Upgrade header if any.
    // The record can trail the client by a moment, so read it again until it holds a whole head, for up to 10 s.
    string received = Encoding.ASCII.GetString(server.Exchanges[^1].Received);
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); !received.Contains("\r\n\r\n") && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(100);
        received = Encoding.ASCII.GetString(server.Exchanges[^1].Received);
    }

    string[] head = received.Split("\r\n");
    string? upgrade = head.FirstOrDefault(l => l.StartsWith("Upgrade:", StringComparison.OrdinalIgnoreCase));
    bool close = response.Headers.Any(h => h.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) && h.Value == "close");
    Console.WriteLine($"{version}: answered in {response.Version}, {response.StatusCode}{(close ? ", Connection: close" : "")}");
    Console.WriteLine($"  server received: {head[0]}{(upgrade is null ? "" : $" ({upgrade})")}");
}

return 0;
