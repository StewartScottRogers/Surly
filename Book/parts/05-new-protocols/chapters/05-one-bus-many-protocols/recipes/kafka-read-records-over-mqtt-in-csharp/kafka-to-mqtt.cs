// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Produces a Kafka record with Curl.Api and reads it as an MQTT 5 subscriber with Curl.Api, on one in-process surl.
// Run: dotnet run kafka-to-mqtt.cs [-- <case>]
//   (no argument)  produce one record to shop.orders, subscribe to shop/orders
//   latest         produce two records; the subscriber and a Kafka consumer each read the topic
//   names          read the store by each spelling, and consume the MQTT spelling over Kafka
//   no-filter      the same produce to a server with no Topics filter
//   no-anonymous   a producer with no login, on a server without AllowAnonymous
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "transfer";

// Kafka and MQTT 5 on one message bus. Topics keeps the last message under shop/ (--topic); the extra argument
// ends an MQTT 5 subscribe once the kept messages are sent.
var options = new SurlServerOptions
{
    ListenUrls = { "kafka://127.0.0.1:0", "mqtt5://127.0.0.1:0" },
    AllowAnonymous = mode != "no-anonymous",
    AdditionalArguments = { "--mqtt5-disconnect-after-retained" },
};
if (mode != "no-filter")
{
    options.Topics.Add("shop/#;retain");
}

await using SurlServer server = await SurlServer.StartAsync(options);
string kafka = server.BoundUrls[0].ToString();
string mqtt5 = server.BoundUrls[1].ToString();
await using CurlClient client = CurlClient.Create();

try
{
    await Produce("order-1001 placed");
    if (mode == "latest")
    {
        await Produce("order-1002 placed");
    }

    Console.WriteLine($"MQTT subscriber of shop/orders: [{await Read(mqtt5 + "shop/orders")}]");
    if (mode == "latest")
    {
        Console.WriteLine($"Kafka consumer of shop.orders: [{(await Read(kafka + "shop.orders")).Replace("\n", "|")}]");
    }
}
catch (CurlException refused)
{
    Console.WriteLine($"refused: {(int)refused.Code} {refused.Code}: {refused.Message}");
}

if (mode == "names")
{
    // The store names topics the MQTT way: the Kafka topic shop.orders is shop/orders there.
    SurlLogPage log = await server.Messages.ReadLogAsync("shop/orders", 0, cancellationToken: CancellationToken.None);
    Console.WriteLine($"ReadLogAsync(\"shop/orders\"): {string.Join(", ", log.Records.Select(r => $"{r.Offset} {Encoding.UTF8.GetString(r.Payload.Span)}"))}");
    try
    {
        await server.Messages.ReadLogAsync("shop.orders", 0, cancellationToken: CancellationToken.None);
    }
    catch (SurlNotFoundException missing)
    {
        Console.WriteLine($"ReadLogAsync(\"shop.orders\"): {missing.Message}");
    }

    try
    {
        await Read(kafka + "shop/orders");
    }
    catch (CurlException refused)
    {
        Console.WriteLine($"Kafka consumer of shop/orders: {(int)refused.Code} {refused.Code}: {refused.Message}");
    }
}

return 0;

// A Kafka produce: a POST of the record's value to the topic.
async Task Produce(string value)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Post, kafka + "shop.orders") { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(value)) },
        CancellationToken.None);
    await response.Body.DisposeAsync();
}

// A GET: over MQTT 5 a subscribe, which is sent the kept message and ends; over Kafka a consume from offset 0.
async Task<string> Read(string url)
{
    using var ceiling = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), ceiling.Token);
    using var reader = new StreamReader(response.Body);
    return await reader.ReadToEndAsync(ceiling.Token);
}
