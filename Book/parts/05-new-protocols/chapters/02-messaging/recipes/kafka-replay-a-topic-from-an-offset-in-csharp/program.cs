// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Replays a Kafka topic from an offset with Surl.Api and Curl.Api, in one process.
// Run: dotnet run program.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// The broker, in-process. AllowAnonymous is --allow-anonymous: no login, for a test.
await using SurlServer server = await Start();
await using CurlClient client = CurlClient.Create();

// The consumer has read the records at offsets 0 to 2 and saved the offset 3.
foreach (string value in new[] { "a", "b", "c" }) await Produce(server, value);
await Produce(server, "d");
await Produce(server, "e");

await Consume("saved offset 3", server, "3");
await Consume("no offset", server, null);
await Consume("offset 0", server, "0");
await Consume("offset 2", server, "2");
await Consume("caught up at 5", server, "5");
await Produce(server, "f");
await Consume("resume at 5", server, "5");
await Consume("past the end at 7", server, "7");
await Consume("not a number", server, "abc");

// The server side sees offsets, which curl never prints.
SurlLogPage page = await server.Messages.ReadLogAsync("orders", fromOffset: 4, cancellationToken: CancellationToken.None);
Console.WriteLine($"log from 4: {string.Join(" ", page.Records.Select(r => $"{r.Offset}={Encoding.UTF8.GetString(r.Payload.Span)}"))}, next {page.NextOffset}");
try
{
    await server.Messages.ReadLogAsync("orders", fromOffset: 7, cancellationToken: CancellationToken.None);
}
catch (SurlReadRefusedException) { Console.WriteLine("log from 7: SurlReadRefusedException"); }

// A new server has a new, empty log, so the saved offset 6 is past its end.
await using SurlServer restarted = await Start();
await Consume("after a restart at 6", restarted, "6");
return 0;

static Task<SurlServer> Start() =>
    SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "kafka://127.0.0.1:0" }, AllowAnonymous = true });

// The server is given its records directly: PublishAsync appends to the topic, as a producer would.
static Task Produce(SurlServer on, string value) =>
    on.Messages.PublishAsync("orders", Encoding.UTF8.GetBytes(value));

// A GET is a consume from the offset in the query, or from 0 without one, to the end of the log.
async Task Consume(string label, SurlServer from, string? offset)
{
    string query = offset is null ? "" : $"?offset={offset}";
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{from.BoundUrl}orders{query}"), CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        Console.WriteLine($"{label}: [{string.Join(", ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries))}]");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
    }
}
