// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test reads a Kafka topic's log after the code under test has run: the service produces order events, and the
// test reads the broker's own log with Surl.Api and checks each record's offset, key and value against what it
// expects. Reading takes nothing from the log.
// Run: dotnet run check-log.cs -- <scenario>
//   solution      four events produced, the log read and compared
//   fields        each record's key and timestamp
//   pages         the log read two records at a time
//   kept          the log read twice, and consumed with Curl.Api afterwards
//   dropped       the service skips an event; the test fails and names it
//   wrong-value   the service writes the wrong status; the test fails and names the offset
//   past-the-end  a read from an offset past the end
//   no-topic      a read of a topic nobody wrote to
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "solution";

// The broker, in-process. AllowAnonymous is --allow-anonymous: no login, for a test.
await using SurlServer server = await SurlServer.StartAsync(
    new SurlServerOptions { ListenUrls = { "kafka://127.0.0.1:0" }, AllowAnonymous = true });

// The code under test: a service that writes one event for each step of an order. A real service would use a
// Kafka client; Curl.Api cannot produce yet, so the events reach the log through the server's PublishAsync.
string[] steps = ["created", "paid", "packed", "shipped"];
for (int step = 0; step < steps.Length; step++)
{
    if (scenario == "dropped" && steps[step] == "packed")
    {
        continue;
    }

    string status = scenario == "wrong-value" && steps[step] == "paid" ? "refunded" : steps[step];
    await server.Messages.PublishAsync("orders", Encoding.UTF8.GetBytes($"order-1001:{status}"));
}

// What the test expects the log to hold, oldest first.
string[] expected = [.. steps.Select(step => $"order-1001:{step}")];

switch (scenario)
{
    case "solution":
    case "dropped":
    case "wrong-value":
        return await Compare();
    case "fields":
        foreach (SurlLogRecord record in (await Read(0)).Records)
        {
            Console.WriteLine($"offset {record.Offset}: key {(record.Key.HasValue ? "set" : "none")}, value {Text(record)}");
        }

        SurlLogPage all = await Read(0);
        Console.WriteLine($"timestamps in order: {all.Records.Zip(all.Records.Skip(1), (a, b) => a.Timestamp <= b.Timestamp).All(ordered => ordered)}");
        break;
    case "pages":
        long next = 0;
        bool more = true;
        while (more)
        {
            SurlLogPage page = await server.Messages.ReadLogAsync("orders", fromOffset: next, limit: 2, cancellationToken: CancellationToken.None);
            Console.WriteLine($"page from {next}: {string.Join(", ", page.Records.Select(Text))}, next {page.NextOffset}, more {page.HasMore}");
            next = page.NextOffset;
            more = page.HasMore;
        }

        break;
    case "kept":
        Console.WriteLine($"first read: {(await Read(0)).Records.Count} records");
        Console.WriteLine($"second read: {(await Read(0)).Records.Count} records");
        await using (CurlClient client = CurlClient.Create())
        {
            CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}orders?offset=2"), CancellationToken.None);
            await using Stream body = response.Body;
            Console.WriteLine($"consumer from offset 2: [{string.Join(", ", (await new StreamReader(body).ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries))}]");
        }

        Console.WriteLine($"third read: {(await Read(0)).Records.Count} records");
        break;
    case "past-the-end":
        try
        {
            await Read(9);
        }
        catch (SurlReadRefusedException refused)
        {
            Console.WriteLine($"read from 9: SurlReadRefusedException: {refused.Message}");
        }

        break;
    case "no-topic":
        try
        {
            SurlLogPage none = await server.Messages.ReadLogAsync("refunds", fromOffset: 0, cancellationToken: CancellationToken.None);
            Console.WriteLine($"refunds: {none.Records.Count} records, next {none.NextOffset}");
        }
        catch (Exception failure) when (failure is SurlNotFoundException or SurlReadRefusedException)
        {
            Console.WriteLine($"refunds: {failure.GetType().Name}: {failure.Message}");
        }

        break;
}

return 0;

Task<SurlLogPage> Read(long from) => server.Messages.ReadLogAsync("orders", fromOffset: from, cancellationToken: CancellationToken.None);

static string Text(SurlLogRecord record) => Encoding.UTF8.GetString(record.Payload.Span);

// The check: read the whole log, list it, and name what is missing and what is not expected.
async Task<int> Compare()
{
    SurlLogPage page = await Read(0);
    string[] found = [.. page.Records.Select(Text)];
    foreach (SurlLogRecord record in page.Records)
    {
        Console.WriteLine($"offset {record.Offset}: {Text(record)}");
    }

    string[] missing = [.. expected.Where(want => !found.Contains(want))];
    string[] unexpected = [.. page.Records.Where(record => !expected.Contains(Text(record))).Select(record => $"{Text(record)} at offset {record.Offset}")];
    if (missing.Length > 0)
    {
        Console.WriteLine($"missing: {string.Join(", ", missing)}");
    }

    if (unexpected.Length > 0)
    {
        Console.WriteLine($"unexpected: {string.Join(", ", unexpected)}");
    }

    bool same = found.SequenceEqual(expected);
    Console.WriteLine(same ? $"the log holds the {expected.Length} events expected, in order" : "FAIL: the log is not what the service should have written");
    return same ? 0 : 1;
}
