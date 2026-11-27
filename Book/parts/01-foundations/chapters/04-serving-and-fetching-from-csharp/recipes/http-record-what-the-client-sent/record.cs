// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Records what a Curl.Api client sent to an in-process Surl.Api server, and what it was answered.
// Run: dotnet run record.cs [-- agent|requests|off]
//   (no argument) one request, then the request head it sent and the server's status line
//   agent         the same with a User-Agent set on the client
//   requests      two requests on one client, then how many exchanges were recorded
//   off           recording left off, then what reading Exchanges does
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "head";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.RecordExchanges = mode != "off";
await using var server = await SurlServer.StartAsync(options);

var clientOptions = new CurlClientOptions { UserAgent = mode == "agent" ? "my-test/1.0" : null };
await using (CurlClient client = CurlClient.Create(clientOptions))
{
    int requests = mode == "requests" ? 2 : 1;
    for (int i = 0; i < requests; i++)
    {
        CurlRequest request = new(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
        {
            Headers = [new("X-Trace", "42")],
        };
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
    }
}

if (mode != "off")
{
    await WaitForEndsAsync();
}

if (mode == "off")
{
    try
    {
        _ = server.Exchanges;
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("recording is off: Exchanges throws InvalidOperationException");
        return 0;
    }

    return 1;
}

if (mode == "requests")
{
    IReadOnlyList<SurlExchangeRecord> exchanges = server.Exchanges;
    Console.WriteLine($"exchanges recorded: {exchanges.Count}");
    foreach (SurlExchangeRecord each in exchanges)
    {
        string text = Encoding.ASCII.GetString(each.Received);
        int gets = text.Split("\r\n").Count(line => line.StartsWith("GET ", StringComparison.Ordinal));
        Console.WriteLine($"exchange {each.ExchangeId}: {gets} requests");
    }

    return 0;
}

// The first exchange: the bytes the server received, then the first line of what it sent.
SurlExchangeRecord exchange = server.Exchanges[0];
Console.WriteLine($"exchange {exchange.ExchangeId} on {exchange.Scheme}, truncated: {exchange.IsTruncated}");
string received = Encoding.ASCII.GetString(exchange.Received);
string head = received[..received.IndexOf("\r\n\r\n", StringComparison.Ordinal)];
foreach (string line in head.Split("\r\n"))
{
    Console.WriteLine($"> {line}");
}

string sent = Encoding.ASCII.GetString(exchange.Sent);
Console.WriteLine($"< {sent[..sent.IndexOf("\r\n", StringComparison.Ordinal)]}");
return 0;

// The server records an exchange a moment after the client has gone, so wait (at most 10 s) until
// every exchange it started has ended before reading the record.
async Task WaitForEndsAsync()
{
    var started = new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted };
    var ended = new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded };
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); server.FindEvents(ended).Count < server.FindEvents(started).Count && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(100);
    }
}
