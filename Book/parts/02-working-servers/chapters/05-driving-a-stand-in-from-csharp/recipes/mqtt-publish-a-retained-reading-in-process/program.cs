// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test publishes a retained reading on an in-process MQTT broker with Messages.PublishAsync, and a
// later subscriber, a Curl.Api client, is sent it as soon as it subscribes.
// Run: dotnet run program.cs [-- binary|large|replaced|rooms|cleared|never|curl-publish|closed]
//   (no argument)  a JSON reading, read by the subscriber and from the store
//   binary         a payload of bytes that are not text
//   large          a payload of 100,000 bytes
//   replaced       a second reading on the same topic
//   rooms          one reading per room, read with a wildcard
//   cleared        an empty payload takes the reading away
//   never          a topic nothing was published on
//   curl-publish   a client publishes on the same topic
//   closed         a broker with no AllowAnonymous
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "solution";

// The broker: no --topic filter at all, a client lets in with no login (unless the case is closed),
// and its events are recorded, so that the test can wait for the broker, not for a clock.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt://127.0.0.1:0" },
    AllowAnonymous = which != "closed",
    RecordExchanges = true,
};
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
const string topic = "home/kitchen/temp";

try
{
    switch (which)
    {
        case "binary":
            byte[] bytes = [0x00, 0xFF, 0x10, 0x80];
            await Retain(topic, bytes);
            Console.WriteLine($"subscriber: {Convert.ToHexString(Payload(topic, await Subscribe(topic)))}");
            break;
        case "large":
            byte[] big = new byte[100_000];
            new Random(5).NextBytes(big);
            await Retain(topic, big);
            byte[] received = Payload(topic, await Subscribe(topic));
            Console.WriteLine($"subscriber: {received.Length} bytes, identical: {received.SequenceEqual(big)}");
            break;
        case "replaced":
            await Retain(topic, "{\"celsius\":21.5}"u8.ToArray());
            await Retain(topic, "{\"celsius\":22.0}"u8.ToArray());
            byte[] reply = await Subscribe(topic);
            Console.WriteLine($"subscriber: {reply.Length} bytes, {Encoding.UTF8.GetString(Payload(topic, reply))}");
            break;
        case "rooms":
            await Retain("home/kitchen/temp", "21.5"u8.ToArray());
            await Retain("home/hall/temp", "19.0"u8.ToArray());
            await Retain("home/cellar/temp", "12.5"u8.ToArray());
            // A # in the URL is a fragment, so the filter is written %23.
            string all = Encoding.UTF8.GetString(await Subscribe("home/%23"));
            Console.WriteLine($"subscriber: {all.Length} bytes");
            foreach (string t in new[] { "home/cellar/temp", "home/hall/temp", "home/kitchen/temp" })
            {
                Console.WriteLine($"{t}: {all.Contains(t)}");
            }

            break;
        case "cleared":
            await Retain(topic, "21.5"u8.ToArray());
            await Retain(topic, []);
            Console.WriteLine($"subscriber: {(await Subscribe(topic)).Length} bytes");
            Console.WriteLine($"store: {await Kept(topic)}");
            break;
        case "never":
            Console.WriteLine($"subscriber: {(await Subscribe(topic)).Length} bytes");
            Console.WriteLine($"store: {await Kept(topic)}");
            break;
        case "curl-publish":
            await Retain(topic, "21.5"u8.ToArray());
            // A client's publish has the retain flag clear. It is not acknowledged, so wait for the broker
            // to finish with the exchange, with a bound, and only then read the store.
            long last = Last();
            await client.SendAsync(new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}{topic}")
            {
                Body = CurlContent.FromBytes("99.9"u8.ToArray()),
            }, CancellationToken.None);
            await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = last }, TimeSpan.FromSeconds(10));
            Console.WriteLine($"store: {await Kept(topic)}");
            Console.WriteLine($"subscriber: {Encoding.UTF8.GetString(Payload(topic, await Subscribe(topic)))}");
            break;
        case "closed":
            await Retain(topic, "21.5"u8.ToArray());
            Console.WriteLine($"store: {await Kept(topic)}");
            await Subscribe(topic);
            break;
        default:
            await Retain(topic, "{\"room\":\"kitchen\",\"celsius\":21.5}"u8.ToArray());
            byte[] reading = await Subscribe(topic);
            Console.WriteLine($"subscriber: {topic} {Encoding.UTF8.GetString(Payload(topic, reading))}");
            Console.WriteLine($"store: {await Kept(topic)}");
            break;
    }
}
catch (CurlException failure)
{
    // The number curl exits with for a refused connection: 8.
    Console.WriteLine($"subscriber: CurlException {(int)failure.Code} {failure.Code}");
}

return 0;

// The test's own publish: the call sets the retain flag itself, so the broker keeps the message
// with no client, no login and no filter, and it is kept as soon as the call returns.
async Task Retain(string t, byte[] payload) => await server.Messages.PublishAsync(t, payload, true, CancellationToken.None);

// A later subscriber: a GET of the topic's URL is answered with every retained message that matches,
// as the topic's two-byte length, the topic and the payload, and it ends by itself.
async Task<byte[]> Subscribe(string filter)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{filter}"), CancellationToken.None);
    await using Stream body = response.Body;
    using var all = new MemoryStream();
    await body.CopyToAsync(all);
    return all.ToArray();
}

// The payload of a single message: what follows the length and the topic.
static byte[] Payload(string t, byte[] reply) => reply[(2 + t.Length)..];

// What the broker holds for a topic, read without a client.
async Task<string> Kept(string t)
{
    SurlRetainedMessage? kept = await server.Messages.ReadRetainedAsync(t, CancellationToken.None);
    return kept is null ? "nothing" : $"{Encoding.UTF8.GetString(kept.Payload.Span)} at QoS {kept.QualityOfService}";
}

long Last() => server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).Select(e => (long)e.Sequence).DefaultIfEmpty(0).Max();
