// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a sensor-to-alert flow with Surl.Api and Curl.Api in one process: the test publishes a reading
// over MQTT as the sensor would, runs the service, which mails an alert past a limit, and checks the mail the
// server received.
// Run: dotnet run program.cs -- <scenario>
//   solution   the boiler reads 81.5 against a limit of 75
//   quiet      readings at or under the limit, and a sensor that never published
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Globalization;
using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string scenario = args.Length > 0 ? args[0] : "solution";

// One server is the broker and the mail server: MQTT for the sensor and the service, SMTP for the alert.
// Topics keeps the latest message on every topic under sensors/. AllowAnonymous is --allow-anonymous.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt://127.0.0.1:0", "smtp://127.0.0.1:0" },
    AllowAnonymous = true,
    RecordExchanges = true,
};
options.Topics.Add("sensors/#;retain");
await using SurlServer server = await SurlServer.StartAsync(options);
Uri mqtt = server.BoundUrls[0], smtp = server.BoundUrls[1];
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(10) });

if (scenario == "solution")
{
    await Publish("boiler", "81.5");
    Console.WriteLine($"service: {await Service("boiler", 75)}");

    // The check: wait, at most 10 s, for the mail store to report a delivery, then read the message it names,
    // owner/INBOX/;UID=n.
    var delivered = (StoreChangedEvent)await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail }, TimeSpan.FromSeconds(10));
    string owner = delivered.Name[..delivered.Name.IndexOf("/INBOX/", StringComparison.Ordinal)];
    SurlMessagePage inbox = await server.Mail.ListMessagesAsync(owner, "INBOX");
    SurlMailMessage alert = await server.Mail.ReadMessageAsync(owner, "INBOX", inbox.Messages[0].Uid);
    string[] lines = Encoding.ASCII.GetString(alert.Bytes.Span).Split("\r\n");
    Console.WriteLine($"alert: {string.Join(" | ", lines.Where(l => !l.StartsWith("Received:", StringComparison.Ordinal) && l.Length > 0))}");
    Console.WriteLine($"mailbox: {inbox.Messages.Count} message(s)");

    SurlRetainedMessage? kept = await server.Messages.ReadRetainedAsync("sensors/boiler/temp");
    Console.WriteLine($"kept: {(kept is null ? "nothing" : Encoding.UTF8.GetString(kept.Payload.Span))}");
}
else
{
    await Publish("boiler", "64.0");
    Console.WriteLine($"service: {await Service("boiler", 75)}");
    await Publish("kiln", "75");
    Console.WriteLine($"service: {await Service("kiln", 75)}");
    Console.WriteLine($"deliveries: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail }).Count}");
    Console.WriteLine($"service: {await Service("pump", 75)}");
}

return 0;

// The sensor: a POST to the topic's URL publishes the body at QoS 0, which the broker does not acknowledge, so
// the call can return before the reading is kept. Wait, at most 10 s, for the exchange to end: by then the
// broker has applied it. Take the last sequence before the POST, or the wait could miss an exchange that ended.
async Task Publish(string sensor, string reading)
{
    long last = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).Select(e => (long)e.Sequence).DefaultIfEmpty(0).Max();
    CurlResponse published = await client.SendAsync(
        new CurlRequest(HttpMethod.Post, $"{mqtt}sensors/{sensor}/temp") { Body = Encoding.ASCII.GetBytes(reading) }, CancellationToken.None);
    await published.Body.DisposeAsync();
    await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = last }, TimeSpan.FromSeconds(10));
}

// The service under test: reads a sensor's latest reading, trying five times a second apart, and past the limit
// mails ops@example.com. A subscriber is sent the topic's length in two bytes, the topic, then the payload.
async Task<string> Service(string sensor, double limit)
{
    string topic = $"sensors/{sensor}/temp", reading = "";
    for (int attempt = 0; attempt < 5 && reading.Length == 0; attempt++)
    {
        if (attempt > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{mqtt}{topic}"), CancellationToken.None);
        await using Stream body = response.Body;
        using var all = new MemoryStream();
        await body.CopyToAsync(all);
        reading = Encoding.ASCII.GetString(all.ToArray().Skip(2 + topic.Length).ToArray());
    }

    if (reading.Length == 0)
    {
        return $"no reading from {sensor}";
    }

    if (double.Parse(reading, CultureInfo.InvariantCulture) <= limit)
    {
        return $"no alert for {sensor} at {reading}";
    }

    var mail = new CurlRequest(HttpMethod.Put, $"{smtp}")
    {
        Mail = new CurlMailOptions { MailFrom = "sensors@example.com", MailRecipients = ["ops@example.com"] },
        Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(
            $"From: sensors@example.com\r\nTo: ops@example.com\r\nSubject: {sensor} at {reading} C\r\n\r\n{sensor} reads {reading} C, past the limit of {limit} C.\r\n"))),
    };
    CurlResponse sent = await client.SendAsync(mail, CancellationToken.None);
    await sent.Body.DisposeAsync();
    return $"alert sent for {sensor} at {reading}";
}
