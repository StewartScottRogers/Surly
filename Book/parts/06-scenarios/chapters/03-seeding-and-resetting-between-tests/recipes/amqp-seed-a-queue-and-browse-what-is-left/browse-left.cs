// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that seeds jobs into an AMQP queue, lets the worker take some, and browses the queue for
// exactly the jobs left, all against an in-process Surl server.
// Run: dotnet run browse-left.cs [-- <case>]
//   (no argument)  five jobs seeded, the worker takes two; job-3 to job-5 are left
//   greedy         the worker takes three; the test expects three left and fails
//   unknown-queue  the seed names a queue the server does not have
//   no-plaintext   the server keeps its default: no password in clear without TLS
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "right";

// The server: AMQP on a free port, one queue, one account for the worker.
var options = new SurlServerOptions
{
    ListenUrls = { "amqp://127.0.0.1:0" },
    AllowPlaintextAuthentication = mode != "no-plaintext",
};
options.Queues.Add("jobs");
options.Accounts.Add(new SurlAccount("worker", "s3cret"));
await using var server = await SurlServer.StartAsync(options);

// The seed: five jobs, put straight into the queue, before the worker runs.
try
{
    for (int n = 1; n <= 5; n++)
    {
        await server.Messages.EnqueueAsync(mode == "unknown-queue" ? "job" : "jobs", Encoding.UTF8.GetBytes($"job-{n}"));
    }
}
catch (SurlSeedRefusedException refused)
{
    Console.WriteLine($"seed refused: {refused.Store}, {refused.Refusal}: {refused.Message}");
    return 1;
}

// The worker under test: here it takes jobs over AMQP with Curl.Api, as curl amqp://... does.
int take = mode == "greedy" ? 3 : 2;
await using (CurlClient client = CurlClient.Create())
{
    for (int n = 0; n < take; n++)
    {
        var get = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}%2F/jobs")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "worker", Password = "s3cret" },
        };
        try
        {
            CurlResponse response = await client.SendAsync(get, CancellationToken.None);
            await using Stream body = response.Body;
            Console.WriteLine($"worker took {await new StreamReader(body).ReadToEndAsync()}");
        }
        catch (CurlException failed)
        {
            Console.WriteLine($"worker failed: curl error {(int)failed.Code}: {failed.Message}");
            return 1;
        }
    }
}

// The check: browse what is left, without taking anything, and compare with what the test expects.
SurlQueuePage page = await server.Messages.BrowseQueueAsync("jobs", -1, 100, CancellationToken.None);
string[] left = [.. page.Messages.Select(m => Encoding.UTF8.GetString(m.Payload.Span))];
string[] want = ["job-3", "job-4", "job-5"];
Console.WriteLine($"left: {string.Join(", ", left)} (ready {page.ReadyCount}, in flight {page.InFlightCount})");
if (!left.SequenceEqual(want))
{
    Console.WriteLine($"FAIL: want {string.Join(", ", want)}");
    return 1;
}

// Browsing took nothing: a second look finds the same three.
Console.WriteLine($"browsed again: ready {(await server.Messages.BrowseQueueAsync("jobs", -1, 100, CancellationToken.None)).ReadyCount}");

// Between tests: reset returns the queue to its state at start, empty.
await server.ResetAsync();
Console.WriteLine($"after reset: ready {(await server.Messages.BrowseQueueAsync("jobs", -1, 100, CancellationToken.None)).ReadyCount}");
return 0;
