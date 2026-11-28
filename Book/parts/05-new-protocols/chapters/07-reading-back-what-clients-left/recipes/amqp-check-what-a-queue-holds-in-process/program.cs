// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test checks what an app queued without taking it off the queue: the app puts jobs on the queue over STOMP,
// and the test browses the queue from the Surl.Api server it runs in. Run: dotnet run program.cs [-- case]
//   (no argument)  the app queues three jobs; the browse finds them in order, twice
//   paging         the browse takes the queue two at a time
//   taken          a worker takes one job over AMQP; the browse finds the other two
//   wrong-queue    the app queues to a name the server does not have
//   no-anonymous   the server keeps its default and refuses the app, which has no login
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "queued";

// One server, one bus: the app queues over STOMP, a worker reads over AMQP, and both reach the queue jobs.
var options = new SurlServerOptions
{
    ListenUrls = { "stomp://127.0.0.1:0", "amqp://127.0.0.1:0" },
    AllowAnonymous = which != "no-anonymous",
};
options.Queues.Add("jobs");
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The app under test queues three jobs, in this order.
string[] queued = ["resize cat.png", "resize dog.png", "mail ann"];
string destination = which == "wrong-queue" ? "job" : "jobs";
try
{
    foreach (string job in queued)
    {
        await Send(destination, job);
    }

    Console.WriteLine($"app queued {queued.Length} jobs");
}
catch (CurlException refused)
{
    Console.WriteLine($"app could not queue: curl error {(int)refused.Code}");
}

if (which == "taken")
{
    // A worker takes the first job off the queue, as curl does with amqp://.
    CurlResponse took = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrls[1]}%2F/jobs"), CancellationToken.None);
    await using Stream body = took.Body;
    Console.WriteLine($"worker took {await new StreamReader(body).ReadToEndAsync()}");
    queued = queued[1..];  // what the test now expects
}

if (which == "paging")
{
    // Two at a time: after_position -1 is the start, and each page names the position to go on from.
    int after = -1;
    while (true)
    {
        SurlQueuePage page = await Browse(after, 2);
        Console.WriteLine($"page after {after}: {string.Join(", ", page.Messages.Select(m => $"{m.Position} {Text(m)}"))} (more: {page.HasMore})");
        if (!page.HasMore)
        {
            return 0;
        }

        after = page.Messages[^1].Position;
    }
}

// The check: what the queue holds, in order, without taking anything.
SurlQueuePage held = await Browse(-1, 100);
Console.WriteLine($"browse: ready {held.ReadyCount}, in flight {held.InFlightCount}");
foreach (SurlQueueMessage message in held.Messages)
{
    Console.WriteLine($"  {message.Position} {Text(message)}");
}

SurlQueuePage again = await Browse(-1, 100);
Console.WriteLine($"browse again: ready {again.ReadyCount}");

bool same = held.Messages.Select(Text).SequenceEqual(queued);
Console.WriteLine($"queue holds exactly what the test expects: {same}");
if (which == "wrong-queue")
{
    // The server made a queue of the app's name: the three jobs are there, in a queue nobody reads.
    SurlQueuePage strayed = await server.Messages.BrowseQueueAsync("job", -1, 100, CancellationToken.None);
    Console.WriteLine($"queue job: ready {strayed.ReadyCount}");
}

return same ? 0 : 1;

// Browsing reads the queue and takes nothing; the page is a snapshot, in delivery order.
Task<SurlQueuePage> Browse(int afterPosition, int limit) =>
    server.Messages.BrowseQueueAsync("jobs", afterPosition, limit, CancellationToken.None);

static string Text(SurlQueueMessage message) => Encoding.UTF8.GetString(message.Payload.Span);

// The app's side: one STOMP SEND to /queue/<name>. The URL path is the destination, the body is the message.
async Task Send(string queue, string text)
{
    var send = new CurlRequest(HttpMethod.Post, $"{server.BoundUrls[0]}queue/{queue}")
    {
        Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes(text)),
    };
    CurlResponse response = await client.SendAsync(send, CancellationToken.None);
    await response.Body.DisposeAsync();
}
