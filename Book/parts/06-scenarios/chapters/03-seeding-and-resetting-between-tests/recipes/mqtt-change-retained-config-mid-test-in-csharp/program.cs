// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds a device's retained config through Surl.Api, changes it mid-test, and reads it as the device would through Curl.Api.
// Run: dotnet run program.cs [-- case]
//   (no argument)    seed, device reads, change, device reads again
//   twice            two changes in a row, then a device connects
//   topics           a change to one topic leaves another's
//   clear            an empty message clears the config
//   filter           a topic outside --topic, with a curl publish and with a seeded one
//   no-retain        a broker with no ;retain at all
//   restart          a broker with a data directory, stopped and started again
//   closed           a broker with no --allow-anonymous
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "change";

// The broker: it keeps what is published under devices/ and lets a device in with no login.
SurlServerOptions Options()
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "mqtt://127.0.0.1:0" },
        AllowAnonymous = which != "closed",
        RecordExchanges = true,
    };
    if (which != "no-retain")
    {
        options.Topics.Add("devices/#;retain");
    }

    return options;
}

await using var server = await SurlServer.StartAsync(Options());

// The device: one Curl.Api client, and its read of a config topic is a GET of the topic's URL.
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "twice":
        await Seed("devices/door/config", "interval=30");
        await Seed("devices/door/config", "interval=5");
        await Device("devices/door/config");
        break;
    case "topics":
        await Seed("devices/lamp/config", "interval=15");
        await Seed("devices/door/config", "interval=5");
        await Seed("devices/door/config", "interval=10");
        await Device("devices/lamp/config");
        await Device("devices/door/config");
        break;
    case "clear":
        await Seed("devices/lamp/config", "interval=60");
        await Seed("devices/lamp/config", "");
        await Device("devices/lamp/config");
        Console.WriteLine($"kept: {await Kept("devices/lamp/config")}");
        break;
    case "filter":
        // A client's publish has the retain flag clear, so only the filter keeps it; a seed sets the flag itself.
        await Publish("lab/lamp/config", "interval=60");
        Console.WriteLine($"curl publish, kept: {await Kept("lab/lamp/config")}");
        await Seed("lab/lamp/config", "interval=60");
        Console.WriteLine($"seeded, kept: {await Kept("lab/lamp/config")}");
        break;
    case "no-retain":
        await Publish("devices/lamp/config", "interval=60");
        Console.WriteLine($"curl publish, kept: {await Kept("devices/lamp/config")}");
        await Seed("devices/lamp/config", "interval=60");
        Console.WriteLine($"seeded, kept: {await Kept("devices/lamp/config")}");
        break;
    case "restart":
        return await Restart();
    case "closed":
        return await Closed();
    default:
        // The seed, before the test: the retain flag is set by the call itself.
        await Seed("devices/lamp/config", "interval=60");
        await Device("devices/lamp/config");

        // The change, in the middle of the test: the new message replaces the old on the topic.
        await Seed("devices/lamp/config", "interval=15");
        await Device("devices/lamp/config");
        Console.WriteLine($"kept: {await Kept("devices/lamp/config")}");
        break;
}

return 0;

// Seeds a retained message as a client's publish with RETAIN set would, with no client and no login.
async Task Seed(string topic, string text, SurlServer? on = null) =>
    await (on ?? server).Messages.PublishAsync(topic, Encoding.UTF8.GetBytes(text), true, CancellationToken.None);

// What the device reads: every message the broker sends when it subscribes, as the text after the topic's two-byte length and name.
async Task Device(string topic)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{topic}");
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    using var bytes = new MemoryStream();
    await body.CopyToAsync(bytes);
    string text = Encoding.UTF8.GetString(bytes.ToArray());
    int header = 2 + topic.Length;
    Console.WriteLine($"{topic}: device reads {(text.Length == 0 ? "nothing" : text[header..])}");
}

// A publish as the device's own client makes it, with the retain flag clear. It is QoS 0, never
// acknowledged, so the test waits for the broker to end that first exchange before reading the store.
async Task Publish(string topic, string text)
{
    var request = new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}{topic}") { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(text)) };
    await client.SendAsync(request, CancellationToken.None);
    await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = 0 }, TimeSpan.FromSeconds(10));
}

// What the broker holds for a topic, read without a client and without dropping it.
async Task<string> Kept(string topic)
{
    SurlRetainedMessage? kept = await server.Messages.ReadRetainedAsync(topic, CancellationToken.None);
    return kept is null ? "nothing" : $"{Encoding.UTF8.GetString(kept.Payload.Span)} at QoS {kept.QualityOfService}";
}

// With a data directory the retained messages are on disk: a second server on the same directory still has them.
async Task<int> Restart()
{
    string data = Directory.CreateTempSubdirectory("retained-").FullName;
    try
    {
        SurlServerOptions first = Options();
        first.DataDirectory = data;
        await using (var before = await SurlServer.StartAsync(first))
        {
            await Seed("devices/lamp/config", "interval=60", before);
        }

        SurlServerOptions second = Options();
        second.DataDirectory = data;
        await using var after = await SurlServer.StartAsync(second);
        SurlRetainedMessage? kept = await after.Messages.ReadRetainedAsync("devices/lamp/config", CancellationToken.None);
        Console.WriteLine($"after the restart: {(kept is null ? "nothing" : Encoding.UTF8.GetString(kept.Payload.Span))}");
        return 0;
    }
    finally
    {
        Directory.Delete(data, true);
    }
}

// The seed needs no login, but the device does: a broker with no AllowAnonymous refuses its connection.
async Task<int> Closed()
{
    await Seed("devices/lamp/config", "interval=60");
    try
    {
        await Device("devices/lamp/config");
        return 0;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
        return (int)failure.Code;
    }
}
