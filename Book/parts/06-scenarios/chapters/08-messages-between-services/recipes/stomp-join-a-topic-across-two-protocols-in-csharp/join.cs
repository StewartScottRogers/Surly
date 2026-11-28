// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// The old service sends over STOMP and the new one subscribes over MQTT 5, both with Curl.Api, on one in-process surl;
// the test shows a message crossing, and reads what the broker kept.
// Run: dotnet run join.cs -- <case>
//   solution        two orders sent to /topic/orders/created; what the new service on orders/created received
//   whole           the same, every message the new service received
//   kept            the same, then the topic's retained message, read from the broker
//   stomp-retained  the same, then a STOMP subscriber to the topic
//   dotted          the old service sends to /topic/orders.created
//   queue           the old service sends to /queue/orders/created
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Surl.Api;

string which = args[0];

// One broker for both services: STOMP for the old, MQTT 5 for the new, on one message bus. Topics keeps the latest
// message under orders/ (--topic "orders/#;retain").
var options = new SurlServerOptions
{
    ListenUrls = { "stomp://127.0.0.1:0", "mqtt5://127.0.0.1:0" },
    AllowAnonymous = true,
    Topics = { "orders/#;retain" },
};
await using SurlServer server = await SurlServer.StartAsync(options);
string stomp = server.BoundUrls[0].ToString();
string mqtt5 = server.BoundUrls[1].ToString();
await using CurlClient client = CurlClient.Create();

string destination = which switch
{
    "dotted" => "topic/orders.created",
    "queue" => "queue/orders/created",
    _ => "topic/orders/created",
};
List<string>? received = await Join(destination, "orders/created");
if (received is null)
{
    Console.WriteLine($"nothing crossed from /{destination} to orders/created");
    return 1;
}

switch (which)
{
    case "whole":
        Console.Write(string.Concat(received.Select(line => line + "\n")));
        break;
    case "kept":
        // The broker's own record: the topic's retained message, read from the store without a client. The new
        // service has it already, but the store may trail it a moment, so look for at most ten seconds.
        SurlRetainedMessage? kept = null;
        for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(10); await Task.Delay(200))
        {
            kept = await server.Messages.ReadRetainedAsync("orders/created");
            if (kept is not null && Encoding.UTF8.GetString(kept.Payload.Span) == "order 1002\n")
            {
                break;
            }
        }

        Console.Write($"kept on orders/created: {(kept is null ? "nothing\n" : Encoding.UTF8.GetString(kept.Payload.Span))}");
        break;
    case "stomp-retained":
        // A STOMP subscriber to the same topic. Nothing comes, so the test stops waiting after 3 s.
        using (var bytes = new MemoryStream())
        using (var ceiling = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            try
            {
                CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, stomp + "topic/orders/created"), ceiling.Token);
                await using Stream body = response.Body;
                await body.CopyToAsync(bytes, ceiling.Token);
            }
            catch (OperationCanceledException)
            {
            }

            Console.WriteLine($"STOMP subscriber was sent {bytes.Length} bytes in 3 s");
        }

        break;
    default:
        Console.Write(string.Concat(received.Where(line => line.StartsWith("order ")).Select(line => line + "\n")));
        break;
}

return 0;

// The test: starts the new service, has the old one send ping until the new one has it (ten tries a second apart at
// most), then two orders, and waits, ten seconds at most, for the second. Answers every line the new service
// received, or null if nothing crossed.
async Task<List<string>?> Join(string from, string topic)
{
    var lines = new List<string>();
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));

    // The new service: an MQTT 5 subscriber stays subscribed, and its body is each message as it arrives.
    Task service = Task.Run(async () =>
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, mqtt5 + topic), stop.Token);
        await using Stream body = response.Body;
        using var reader = new StreamReader(body);
        while (await reader.ReadLineAsync(stop.Token) is string line)
        {
            lock (lines)
            {
                lines.Add(line);
            }
        }
    });

    bool Has(string line)
    {
        lock (lines)
        {
            return lines.Contains(line);
        }
    }

    bool joined = false;
    for (int attempt = 0; attempt < 10 && !joined; attempt++)
    {
        await Send(from, "ping\n");
        for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(1) && !(joined = Has("ping"));)
        {
            await Task.Delay(50);
        }
    }

    if (joined)
    {
        await Send(from, "order 1001\n");
        await Send(from, "order 1002\n");
        for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(10) && !Has("order 1002");)
        {
            await Task.Delay(50);
        }
    }

    stop.Cancel();
    try
    {
        await service;
    }
    catch (Exception) when (stop.IsCancellationRequested)
    {
    }

    lock (lines)
    {
        return joined ? [.. lines] : null;
    }
}

// The old service: a STOMP send is a POST of the message to the destination.
async Task Send(string to, string message)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Post, stomp + to) { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(message)) },
        CancellationToken.None);
    await using Stream ignored = response.Body;
}
