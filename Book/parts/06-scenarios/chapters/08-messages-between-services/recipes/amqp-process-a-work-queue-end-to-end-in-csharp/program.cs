// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a work queue end to end with Surl.Api and Curl.Api in one process: jobs are queued on a durable
// queue, a worker receives and acknowledges them over STOMP, and the test checks what is left and what the
// server recorded.
// Run: dotnet run program.cs -- <scenario>
//   solution   three jobs, a worker that acknowledges all three
//   restart    two jobs queued, the server restarted on the same folder, a worker takes them
//   fails      three jobs, a worker that acknowledges two of them
//   refused    a server with no AllowAnonymous
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string scenario = args.Length > 0 ? args[0] : "solution";

DirectoryInfo data = Directory.CreateTempSubdirectory("surl-work-");
await using CurlClient client = CurlClient.Create();
SurlServer server = null!;
try
{
    server = await Start(scenario != "refused");

    switch (scenario)
    {
        case "solution":
            await Queue(server, "job-1", "job-2", "job-3");
            Console.WriteLine($"worker received: {await Work(server, acknowledge: 3)}");
            await Check(server);
            break;
        case "restart":
            await Queue(server, "job-4", "job-5");
            await server.DisposeAsync();
            server = await Start(true);
            Console.WriteLine($"after the restart the worker received: {await Work(server, acknowledge: 2)}");
            await Check(server);
            break;
        case "fails":
            await Queue(server, "job-6", "job-7", "job-8");
            Console.WriteLine($"worker received: {await Work(server, acknowledge: 2)}");
            Console.WriteLine($"after the worker: ready {await Browse(server)}");
            Console.WriteLine($"second receive: [{await Receive(server)}]");
            await Check(server);
            break;
        case "refused":
            try
            {
                await Receive(server);
            }
            catch (CurlException refused)
            {
                Console.WriteLine($"receive: CurlException {(int)refused.Code} {refused.Code}: {refused.Message}");
            }

            Console.WriteLine($"worker: {(await Work(server, acknowledge: 3)).Split('\n')[0]}");
            break;
    }

    return 0;
}
finally
{
    await server.DisposeAsync();
    data.Delete(true);
}

// AMQP for the app, STOMP for the worker, one durable queue on a folder. AllowAnonymous is --allow-anonymous.
async Task<SurlServer> Start(bool anonymous)
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "amqp://127.0.0.1:0", "stomp://127.0.0.1:0" },
        AllowAnonymous = anonymous,
        DataDirectory = data.FullName,
        RecordExchanges = true,
    };
    options.Queues.Add("jobs;durable");
    return await SurlServer.StartAsync(options);
}

// The app: Curl.Api cannot publish over AMQP, so the jobs are put on the queue from the server's side.
static async Task Queue(SurlServer server, params string[] jobs)
{
    foreach (string job in jobs)
    {
        await server.Messages.EnqueueAsync("jobs", Encoding.UTF8.GetBytes(job));
    }
}

// The worker: a STOMP session sent as the body of a telnet:// request, which writes back what the server answered.
// It subscribes with per-message acknowledgement, acknowledges the first n messages and disconnects with a receipt.
async Task<string> Work(SurlServer server, int acknowledge)
{
    var frames = new StringBuilder("CONNECT\naccept-version:1.2\nhost:/\n\n\0SUBSCRIBE\nid:0\ndestination:/queue/jobs\nack:client-individual\n\n\0");
    for (int id = 1; id <= acknowledge; id++)
    {
        frames.Append($"ACK\nid:{id}\n\n\0");
    }

    frames.Append("DISCONNECT\nreceipt:bye\n\n\0");
    Uri stomp = server.BoundUrls[1];
    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var request = new CurlRequest(HttpMethod.Put, $"telnet://{stomp.Host}:{stomp.Port}/") { Body = Encoding.UTF8.GetBytes(frames.ToString()) };
    CurlResponse response = await client.SendAsync(request, limit.Token);
    await using Stream body = response.Body;
    string answer = await new StreamReader(body).ReadToEndAsync(limit.Token);
    if (answer.StartsWith("ERROR", StringComparison.Ordinal))
    {
        return answer.Replace("\0", "");
    }

    string jobs = string.Join(", ", Regex.Matches(answer, @"job-\d+").Select(m => m.Value));
    return answer.Contains("RECEIPT", StringComparison.Ordinal) ? $"{jobs}, then the receipt" : jobs;
}

// The test's own receive: Curl.Api takes one message over AMQP; an empty queue gives an empty body.
async Task<string> Receive(SurlServer server)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrls[0]}%2F/jobs"), CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body).ReadToEndAsync();
}

// Browsing counts what is ready, and what a consumer holds, without taking anything. The server puts back
// what a consumer held only once it notices the consumer has gone, so browse until nothing is in flight,
// for at most 10 s.
static async Task<string> Browse(SurlServer server)
{
    SurlQueuePage page = await server.Messages.BrowseQueueAsync("jobs", -1, 100, CancellationToken.None);
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); page.InFlightCount > 0 && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(100);
        page = await server.Messages.BrowseQueueAsync("jobs", -1, 100, CancellationToken.None);
    }

    return $"{page.ReadyCount} [{string.Join(", ", page.Messages.Select(m => Encoding.UTF8.GetString(m.Payload.Span)))}], in flight {page.InFlightCount}";
}

// What the test checks afterwards: the queue, and the server's own record of what happened to the messages.
async Task Check(SurlServer server)
{
    Console.WriteLine($"queue: ready {await Browse(server)}");
    var changes = server.FindEvents(new SurlEventQuery { Store = StoreKind.Bus }).OfType<StoreChangedEvent>().ToList();
    int Count(string change) => changes.Count(c => c.Change.ToString() == change);
    Console.WriteLine($"events: enqueued {Count("Enqueued")}, delivered {Count("Delivered")}, acknowledged {Count("Acknowledged")}");
}
