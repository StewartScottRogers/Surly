// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Two teams' names for one stream: the orders service publishes on the NATS subject orders.created,
// the shop reads the MQTT topic shop/orders/new. One in-process Surl server serves both protocols
// with a topic map that joins the two names, and Curl.Api plays each team's client.
// Run: dotnet run map-topics.cs [-- <case>]
//   (no argument)  a NATS publish to orders.created, read back as the MQTT topic shop/orders/new
//   back           an MQTT publish to shop/orders/new, received by a NATS subscriber to orders.created
//   nomap          the same NATS publish with no map: where it lands
//   wildcard       a map whose NATS side holds a wildcard
//   locked         the map without AllowAnonymous: a NATS publish with no login
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "forth";

// NATS and MQTT on free ports, one message bus, every publish kept for a later MQTT subscriber.
var options = new SurlServerOptions
{
    ListenUrls = { "nats://127.0.0.1:0", "mqtt://127.0.0.1:0" },
    AllowAnonymous = mode != "locked",
    Topics = { "#;retain" },
};
if (mode != "nomap")
{
    options.TopicMaps.Add(mode == "wildcard" ? "nats:orders.*=mqtt:shop/orders/new" : "nats:orders.created=mqtt:shop/orders/new");
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"refused at start, exit {(int)e.ExitCode}: {e.Lines[0]}");
    return 2;
}

await using (server)
{
    string nats = server.BoundUrls[0].ToString(), mqtt = server.BoundUrls[1].ToString();
    await using CurlClient client = CurlClient.Create();
    switch (mode)
    {
        case "forth" or "locked":
            try
            {
                await Publish(client, nats + "orders.created", "paid");
            }
            catch (CurlException e)
            {
                Console.WriteLine($"NATS publish refused: curl {(int)e.Code} {e.Message}");
                return 1;
            }

            Console.WriteLine($"MQTT shop/orders/new: {await Subscribe(client, mqtt + "shop/orders/new")}");
            break;
        case "nomap":
            await Publish(client, nats + "orders.created", "paid");
            foreach (string topic in new[] { "orders/created", "shop/orders/new" })
            {
                // A topic with nothing kept answers an empty body.
                string got = await Subscribe(client, mqtt + topic);
                Console.WriteLine($"MQTT {topic}: {(got == "" ? "(nothing)" : got)}");
            }

            break;
        case "back":
            // NATS keeps nothing for a late subscriber: subscribe first, for one message, and publish
            // over MQTT every 200 ms until it has one, for at most ten seconds.
            using (var bound = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                Task<CurlResponse> subscriber = client.SendAsync(new CurlRequest(HttpMethod.Get, nats + "orders.created?count=1"), bound.Token);
                while (!subscriber.IsCompleted && !bound.IsCancellationRequested)
                {
                    await using CurlClient shop = CurlClient.Create();
                    await Publish(shop, mqtt + "shop/orders/new", "shipped");
                    await Task.WhenAny(subscriber, Task.Delay(200));
                }

                CurlResponse response = await subscriber;
                // The subscriber writes each payload and a newline, as curl does.
                Console.WriteLine($"NATS orders.created: {(await new StreamReader(response.Body).ReadToEndAsync()).TrimEnd('\n')}");
            }

            break;
    }
}

return 0;

// One publish: a POST with the payload as its body (NATS PUB, MQTT PUBLISH).
static async Task Publish(CurlClient client, string url, string payload)
{
    CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Post, url) { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(payload)) }, CancellationToken.None);
    await sent.Body.DisposeAsync();
}

// An MQTT subscribe: the retained message comes as the topic's length in two bytes, the topic and
// the payload, and the server then ends the session. Returns the payload.
static async Task<string> Subscribe(CurlClient client, string url)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
    var bytes = new MemoryStream();
    await response.Body.CopyToAsync(bytes);
    byte[] all = bytes.ToArray();
    return all.Length == 0 ? "" : Encoding.UTF8.GetString(all, 2 + (all[0] << 8 | all[1]), all.Length - 2 - (all[0] << 8 | all[1]));
}
