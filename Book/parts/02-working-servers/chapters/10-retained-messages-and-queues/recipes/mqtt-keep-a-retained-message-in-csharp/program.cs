// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Keeps a retained reading on an in-process MQTT broker, by seeding it with Surl.Api or by a client's publish
// under a declared topic, and reads it as a later subscriber would through Curl.Api.
// Run: dotnet run program.cs [-- case]
//   (no argument)  seed a reading with Messages.PublishAsync, a later subscriber reads it
//   filter         a client publishes under the declared filter, and a later subscriber reads it
//   replaced       a second publish to a topic replaces the first
//   topics         each topic keeps its own reading
//   outside        a topic outside the filter: a client's publish is not kept, a seeded one is
//   no-filter      a broker with no --topic: the same
//   clear          an empty message clears the topic
//   closed         a broker with no AllowAnonymous
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "seed";

// The broker: it keeps what is published under sensors/, lets a client in with no login, and records its
// events so that the test can wait for the broker, not for a clock.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt://127.0.0.1:0" },
    AllowAnonymous = which != "closed",
    RecordExchanges = true,
};
if (which != "no-filter")
{
    options.Topics.Add("sensors/#;retain");
}

await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
long lastSequence = 0;

switch (which)
{
    case "filter":
        await Publish("sensors/kitchen/temp", "21.5");
        Console.WriteLine($"kept: {await Kept("sensors/kitchen/temp")}");
        await Subscribe("sensors/kitchen/temp");
        break;
    case "replaced":
        await Publish("sensors/kitchen/temp", "21.5");
        await Publish("sensors/kitchen/temp", "22.0");
        await Subscribe("sensors/kitchen/temp");
        break;
    case "topics":
        await Publish("sensors/kitchen/temp", "22.0");
        await Publish("sensors/hall/temp", "19.0");
        await Subscribe("sensors/hall/temp");
        await Subscribe("sensors/kitchen/temp");
        break;
    case "outside":
    case "no-filter":
        string topic = which == "outside" ? "lab/kitchen/temp" : "sensors/kitchen/temp";
        await Publish(topic, "21.5");
        Console.WriteLine($"client publish, kept: {await Kept(topic)}");
        await Seed(topic, "21.5");
        Console.WriteLine($"seeded, kept: {await Kept(topic)}");
        await Subscribe(topic);
        break;
    case "clear":
        await Publish("sensors/hall/temp", "19.0");
        await Publish("sensors/hall/temp", "");
        await Subscribe("sensors/hall/temp");
        Console.WriteLine($"kept: {await Kept("sensors/hall/temp")}");
        break;
    case "closed":
        return await Closed();
    default:
        // The seed: the call sets the retain flag itself, so no filter is needed to keep it.
        await Seed("sensors/kitchen/temp", "21.5");
        await Subscribe("sensors/kitchen/temp");
        // A second subscriber is sent it too: reading does not use it up.
        await Subscribe("sensors/kitchen/temp");
        Console.WriteLine($"kept: {await Kept("sensors/kitchen/temp")}");
        break;
}

return 0;

// Seeds a retained message as a client's publish with RETAIN set would, with no client and no login.
async Task Seed(string topic, string text) =>
    await server.Messages.PublishAsync(topic, Encoding.UTF8.GetBytes(text), true, CancellationToken.None);

// A publish as a client makes it: Curl.Api sends it with the retain flag clear. It is not acknowledged, so the
// call can return before the broker has applied it; the end of the client's exchange is recorded after, and
// that is what the test waits for, with a bound.
async Task Publish(string topic, string text)
{
    var request = new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}{topic}") { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(text)) };
    await client.SendAsync(request, CancellationToken.None);
    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
}

// A later subscriber: a GET of the topic's URL answers every retained message, as the topic's two-byte length, the topic and the text.
async Task Subscribe(string topic)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{topic}"), CancellationToken.None);
    await using Stream body = response.Body;
    using var bytes = new MemoryStream();
    await body.CopyToAsync(bytes);
    string text = Encoding.UTF8.GetString(bytes.ToArray());
    Console.WriteLine($"{topic}: subscriber reads {(text.Length == 0 ? "nothing" : text[(2 + topic.Length)..])}");
}

// What the broker holds for a topic, read without a client and without dropping it.
async Task<string> Kept(string topic)
{
    SurlRetainedMessage? kept = await server.Messages.ReadRetainedAsync(topic, CancellationToken.None);
    return kept is null ? "nothing" : $"{Encoding.UTF8.GetString(kept.Payload.Span)} at QoS {kept.QualityOfService}";
}

// The seed needs no login, but the client does: a broker with no AllowAnonymous refuses its connection.
async Task<int> Closed()
{
    await Seed("sensors/kitchen/temp", "21.5");
    try
    {
        await Subscribe("sensors/kitchen/temp");
        return 0;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
        return (int)failure.Code;
    }
}
