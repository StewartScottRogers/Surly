// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Sends over STOMP with Curl.Api and reads the kept message over MQTT with Curl.Api, on one in-process surl.
// Run: dotnet run program.cs [-- case]
//   (no argument)          send on to devices/lamp/state, read it over MQTT
//   latest-wins            send on, then off, to one topic
//   empty-clears           send on, then an empty body, to one topic
//   wildcard               send to two topics, read devices/hall/#
//   outside-the-filter     send to a topic no --topic filter matches
//   no-filter              the same send to a server with no --topic filter
//   queue-is-not-a-topic   send to a /queue/ destination
//   stomp-subscriber       subscribe over STOMP to a topic whose message is kept
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "transfer";

// The server: STOMP and MQTT on one message bus. --topic keeps what is sent under devices/ (Topics); --idle-timeout
// ends a STOMP subscriber once nothing arrives. No account is set up, so both clients are let in anonymously.
var options = new SurlServerOptions
{
    ListenUrls = { "stomp://127.0.0.1:0", "mqtt://127.0.0.1:0" },
    AllowAnonymous = true,
    RecordExchanges = true,
    AdditionalArguments = { "--idle-timeout", "1" },
};
if (which != "no-filter")
{
    options.Topics.Add("devices/#;retain");
}

await using SurlServer server = await SurlServer.StartAsync(options);
string stomp = server.BoundUrls[0].ToString();
string mqtt = server.BoundUrls[1].ToString();
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "latest-wins":
        await Send("topic/devices/fan/state", "on");
        await Send("topic/devices/fan/state", "off");
        await Read("devices/fan/state");
        break;
    case "empty-clears":
        await Send("topic/devices/door/state", "on");
        await Send("topic/devices/door/state", "");
        await Read("devices/door/state");
        break;
    case "wildcard":
        await Send("topic/devices/hall/temp", "21.5");
        await Send("topic/devices/hall/humidity", "55");
        await Read("devices/hall/%23");
        break;
    case "outside-the-filter":
        await Send("topic/other/lamp", "on");
        await Read("other/lamp");
        break;
    case "queue-is-not-a-topic":
        await Send("queue/devices/jobs", "job");
        await Read("devices/jobs");
        break;
    case "stomp-subscriber":
        await Send("topic/devices/lamp/state", "on");
        await Read("devices/lamp/state");
        await Subscribe("topic/devices/lamp/state");
        break;
    default:
        await Send("topic/devices/lamp/state", "on");
        await Read("devices/lamp/state");
        break;
}

return 0;

// A STOMP send: a POST of the body to the destination.
async Task Send(string destination, string body)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Post, stomp + destination) { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(body)) },
        CancellationToken.None);
    await using Stream ignored = response.Body;
    await WaitForEndsAsync();
}

// An MQTT subscribe: what comes back is each retained message as a two-byte topic length, the topic and the payload,
// and the subscribe ends by itself once they are sent. The program prints one line per message and the byte count.
async Task Read(string topic)
{
    byte[] bytes = await Fetch(mqtt + topic);
    var messages = new List<string>();
    for (int at = 0; at < bytes.Length;)
    {
        int length = (bytes[at] << 8) | bytes[at + 1];
        string name = Encoding.UTF8.GetString(bytes, at + 2, length);
        int end = Array.IndexOf(bytes, (byte)0, at + 2 + length);
        end = end < 0 ? bytes.Length : end;
        messages.Add($"{name}: {Encoding.UTF8.GetString(bytes, at + 2 + length, end - at - 2 - length)}");
        at = end;
    }

    foreach (string line in messages.Order())
    {
        Console.WriteLine(line);
    }

    Console.WriteLine($"{messages.Count} retained, {bytes.Length} bytes");
}

// A STOMP subscribe: surl ends the idle subscription after the idle timeout; the token is only a ceiling.
async Task Subscribe(string destination)
{
    byte[] bytes = await Fetch(stomp + destination);
    Console.WriteLine($"STOMP subscriber was sent {bytes.Length} bytes");
}

async Task<byte[]> Fetch(string url)
{
    using var ceiling = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), ceiling.Token);
    await using Stream body = response.Body;
    using var memory = new MemoryStream();
    await body.CopyToAsync(memory, ceiling.Token);
    await WaitForEndsAsync();
    return memory.ToArray();
}

// The server records an exchange a moment after the client has gone, so wait (at most 10 s) until every exchange it
// started has ended, before the next call reads what the last one left.
async Task WaitForEndsAsync()
{
    var started = new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted };
    var ended = new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded };
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); server.FindEvents(ended).Count < server.FindEvents(started).Count && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(50);
    }
}
