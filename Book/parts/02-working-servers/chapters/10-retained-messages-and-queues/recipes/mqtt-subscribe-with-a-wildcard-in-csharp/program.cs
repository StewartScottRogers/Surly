// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds retained readings on several topics of an in-process MQTT broker with Surl.Api and checks, through Curl.Api,
// which ones a wildcard subscription receives and which it misses.
// Run: dotnet run program.cs [-- filter ...]
//   (no argument)  sensors/+/temp
//   <filter>       any filter, written as a subscriber writes it: sensors/#, sensors/kitchen/+, ...
//   <filter> raw   the same, with a # left as it is in the URL, not written %23
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string filter = args.Length > 0 ? args[0] : "sensors/+/temp";
bool raw = args.Length > 1 && args[1] == "raw";

// The broker: no login needed, and six readings seeded as retained. PublishAsync sets the retain flag itself.
var options = new SurlServerOptions { ListenUrls = { "mqtt://127.0.0.1:0" }, AllowAnonymous = true };
await using var server = await SurlServer.StartAsync(options);
var seeded = new Dictionary<string, string>
{
    ["sensors/kitchen/temp"] = "21",
    ["sensors/hall/temp"] = "19",
    ["sensors/kitchen/humidity"] = "40",
    ["sensors/kitchen/oven/temp"] = "180",
    ["sensors/temp"] = "5",
    ["sensors"] = "up",
};
foreach ((string topic, string text) in seeded)
{
    await server.Messages.PublishAsync(topic, Encoding.UTF8.GetBytes(text), true, CancellationToken.None);
}

// The subscriber: one Curl.Api client. A GET of mqtt://host:port/<filter> subscribes to it; a # is written %23,
// since a plain one would be taken as the start of a URL fragment.
await using CurlClient client = CurlClient.Create();
try
{
    Dictionary<string, string> received = await Subscribe(raw ? filter : filter.Replace("#", "%23"));
    Console.WriteLine($"{filter} receives: {Join(received.Keys, received)}");
    Console.WriteLine($"{filter} misses: {Join(seeded.Keys.Except(received.Keys), seeded)}");
    return 0;
}
catch (CurlException failure)
{
    Console.WriteLine($"{filter}: curl error {(int)failure.Code}: {failure.Message}");
    return (int)failure.Code;
}

static string Join(IEnumerable<string> topics, Dictionary<string, string> readings)
{
    string[] sorted = topics.Order(StringComparer.Ordinal).Select(topic => $"{topic}={readings[topic]}").ToArray();
    return sorted.Length == 0 ? "nothing" : string.Join(", ", sorted);
}

// The body is every retained message the broker sends: each as the topic's length in two bytes, the topic, and the
// payload, which runs to the next message's zero byte, since the topics here are shorter than 256 bytes.
async Task<Dictionary<string, string>> Subscribe(string filterInUrl)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{filterInUrl}"), CancellationToken.None);
    await using Stream body = response.Body;
    using var all = new MemoryStream();
    await body.CopyToAsync(all);
    byte[] bytes = all.ToArray();
    var messages = new Dictionary<string, string>();
    for (int at = 0; at < bytes.Length;)
    {
        int length = (bytes[at] << 8) | bytes[at + 1];
        string topic = Encoding.UTF8.GetString(bytes, at + 2, length);
        int start = at + 2 + length;
        int end = Array.IndexOf(bytes, (byte)0, start);
        end = end < 0 ? bytes.Length : end;
        messages[topic] = Encoding.UTF8.GetString(bytes, start, end - start);
        at = end;
    }

    return messages;
}
