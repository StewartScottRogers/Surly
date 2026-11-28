// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that retained readings survive a restart: an in-process MQTT broker keeps them in DataDirectory, a client
// publishes, the test waits for the broker, stops it and starts another on the same folder, and reads the same
// readings back as a subscriber would and with Messages.ReadRetainedAsync.
// Run: dotnet run program.cs [-- case]
//   (no argument)  publish two readings, restart, a subscriber reads the kitchen's
//   every-topic    the restart, then a subscription to sensors/# reads both
//   on-disk        the folder's files, while the broker runs and after it stops
//   outside        a publish outside the declared filter is not there after the restart
//   seeded         a reading seeded with Messages.PublishAsync is there after the restart
//   twice          a new reading after the restart, a second restart, and the subscription
//   memory-holds   the same broker with no DataDirectory, before the restart
//   memory         the same, after the restart
//   in-use         a second broker on the folder while the first runs
//   missing        a DataDirectory that does not exist
//   closed         a broker with no AllowAnonymous
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "restart";

// The folder the retained messages live in: it must exist before the broker starts.
DirectoryInfo data = Directory.CreateTempSubdirectory("surl-readings-");
string retainedFile = Path.Combine(data.FullName, ".surl", "bus", "retained");
string lockFile = Path.Combine(data.FullName, ".surl", "lock");
long lastSequence = 0;

try
{
    bool keep = !which.StartsWith("memory");
    SurlServer server = await Start(keep ? data.FullName : null, which != "closed");

    switch (which)
    {
        case "missing":
            try
            {
                await using SurlServer lost = await Start(Path.Combine(data.FullName, "nosuch"), true);
                Console.WriteLine("started");
            }
            catch (SurlServerStartException refused)
            {
                Console.WriteLine($"exit {(int)refused.ExitCode}, could not open: {refused.Lines[0].Contains("Could not open directory")}");
            }

            await server.DisposeAsync();
            return 0;
        case "closed":
            int code = 0;
            try
            {
                await Publish(server, "sensors/kitchen/temp", "21.5");
            }
            catch (CurlException refused)
            {
                Console.WriteLine($"curl error {(int)refused.Code}: {refused.Message}");
                code = (int)refused.Code;
            }

            // The folder is released before the program deletes it.
            await server.DisposeAsync();
            return code;
        case "seeded":
            // A seed is not a client's publish: the call sets the retain flag itself, and the broker writes it to disk.
            await server.Messages.PublishAsync("sensors/kitchen/temp", Encoding.UTF8.GetBytes("21.5"), true, CancellationToken.None);
            break;
        case "outside":
            await Publish(server, "lab/kitchen/temp", "22.0");
            await Publish(server, "sensors/kitchen/temp", "21.5");
            break;
        default:
            await Publish(server, "sensors/kitchen/temp", "21.5");
            await Publish(server, "sensors/hall/temp", "19.0");
            break;
    }

    if (which == "memory-holds")
    {
        await Subscribe(server, "sensors/kitchen/temp");
        await server.DisposeAsync();
        return 0;
    }

    if (which == "in-use")
    {
        try
        {
            await using SurlServer second = await Start(data.FullName, true);
            Console.WriteLine("second broker: started");
        }
        catch (SurlServerStartException refused)
        {
            Console.WriteLine($"second broker: exit {(int)refused.ExitCode}, in use: {refused.Lines[0].Contains("is in use by another surl process")}");
        }

        await server.DisposeAsync();
        return 0;
    }

    if (which == "on-disk")
    {
        Console.WriteLine($"running: lock {File.Exists(lockFile)}, retained {File.Exists(retainedFile)}");
        await server.DisposeAsync();
        Console.WriteLine($"stopped: lock {File.Exists(lockFile)}, retained {File.Exists(retainedFile)}");
        return 0;
    }

    // The restart: stop the broker, start another on the same folder, with the same options.
    server = await Restart(server, keep);

    switch (which)
    {
        case "every-topic":
            await SubscribeAll(server);
            break;
        case "outside":
            Console.WriteLine($"lab/kitchen/temp, kept: {await Kept(server, "lab/kitchen/temp")}");
            await Subscribe(server, "lab/kitchen/temp");
            Console.WriteLine($"sensors/kitchen/temp, kept: {await Kept(server, "sensors/kitchen/temp")}");
            break;
        case "twice":
            // The restarted broker keeps what is published to it: replace the hall's reading, restart again.
            await Publish(server, "sensors/hall/temp", "20.0");
            server = await Restart(server, keep);
            await SubscribeAll(server);
            break;
        case "seeded":
            Console.WriteLine($"sensors/kitchen/temp, kept: {await Kept(server, "sensors/kitchen/temp")}");
            await Subscribe(server, "sensors/kitchen/temp");
            break;
        default:
            await Subscribe(server, "sensors/kitchen/temp");
            Console.WriteLine($"kept: {await Kept(server, "sensors/kitchen/temp")}");
            break;
    }

    await server.DisposeAsync();
    return 0;
}
finally
{
    data.Delete(true);
}

// The same options every time; the folder is what is kept. RecordExchanges lets the test wait for the broker.
async Task<SurlServer> Start(string? folder, bool anonymous)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "mqtt://127.0.0.1:0" },
        AllowAnonymous = anonymous,
        RecordExchanges = true,
        DataDirectory = folder,
    };
    options.Topics.Add("sensors/#;retain");
    return await SurlServer.StartAsync(options);
}

// A restart: the old broker is disposed, which releases the folder, and the new one is a new server with its own port
// and its own record of events, so the sequence the test waits after starts again at zero.
async Task<SurlServer> Restart(SurlServer old, bool folderKept)
{
    await old.DisposeAsync();
    lastSequence = 0;
    return await Start(folderKept ? data.FullName : null, true);
}

// The app's publish, as a client sends it: Curl.Api publishes with the retain flag clear. It gets no answer at QoS 0,
// so the test waits for the broker to record the end of the exchange, with a bound that only a failure reaches,
// before it stops the broker.
async Task Publish(SurlServer broker, string topic, string reading)
{
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Post, $"{broker.BoundUrl}{topic}") { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(reading)) };
    CurlResponse sent = await client.SendAsync(request, CancellationToken.None);
    await sent.Body.DisposeAsync();
    SurlEvent ended = await broker.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
}

// A subscriber: a GET of the topic's URL answers every retained message, as the topic's two-byte length, the topic
// and the text, and returns by itself once they are sent.
async Task<string> Read(SurlServer broker, string path)
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{broker.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    using var bytes = new MemoryStream();
    await body.CopyToAsync(bytes);
    return Encoding.UTF8.GetString(bytes.ToArray());
}

async Task Subscribe(SurlServer broker, string topic)
{
    string text = await Read(broker, topic);
    Console.WriteLine($"{topic}: subscriber reads {(text.Length == 0 ? "nothing" : text[(2 + topic.Length)..])}");
}

// A subscription to sensors/#, which is sensors/%23 in a URL: every kept topic, one after the other.
async Task SubscribeAll(SurlServer broker)
{
    string text = await Read(broker, "sensors/%23");
    Console.WriteLine($"sensors/#: {text.Length} bytes, hall {text.Contains("\u0011sensors/hall/temp" + (which == "twice" ? "20.0" : "19.0"))}, kitchen {text.Contains("\u0014sensors/kitchen/temp21.5")}");
}

// What the broker holds for a topic, read from its store with no client: null when nothing is kept.
async Task<string> Kept(SurlServer broker, string topic)
{
    SurlRetainedMessage? kept = await broker.Messages.ReadRetainedAsync(topic, CancellationToken.None);
    return kept is null ? "nothing" : $"{Encoding.UTF8.GetString(kept.Payload.Span)} at QoS {kept.QualityOfService}";
}
