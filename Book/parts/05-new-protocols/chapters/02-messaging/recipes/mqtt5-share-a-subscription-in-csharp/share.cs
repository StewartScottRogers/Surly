// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of workers that share one MQTT 5 subscription: two Curl.Api subscriptions to $share/workers/jobs and a
// monitor on jobs, an app that publishes six jobs, and a check that each job went to one worker only.
// Run: dotnet run share.cs [-- <case>]
//   (no argument)  two workers share the jobs; the monitor is sent them all
//   late           the same, then a worker and a plain subscriber that come late
//   no-share-topic a worker subscribes to $share/workers, with no topic filter
//   no-anonymous   a worker with no login, on a broker without AllowAnonymous
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "share";

// An MQTT 5 broker for the workers and an MQTT 3.1.1 listener for the app, on one bus; the last job on jobs is kept.
var options = new SurlServerOptions
{
    ListenUrls = { "mqtt5://127.0.0.1:0", "mqtt://127.0.0.1:0" },
    AllowAnonymous = mode != "no-anonymous",
    Topics = { "jobs;retain" },
};
await using SurlServer server = await SurlServer.StartAsync(options);
string mqtt5 = server.BoundUrls[0].ToString();
await using CurlClient client = CurlClient.Create();

if (mode is "no-share-topic" or "no-anonymous")
{
    string filter = mode == "no-share-topic" ? "%24share/workers" : "%24share/workers/jobs";
    try
    {
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.SendAsync(new CurlRequest(HttpMethod.Get, mqtt5 + filter), bound.Token);
    }
    catch (CurlException refused)
    {
        Console.WriteLine($"refused: {(int)refused.Code} {refused.Code}: {refused.Message}");
    }

    return 0;
}

// Two workers in the group, and a monitor outside it; each keeps the lines it is sent until stopped.
using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var worker1 = new List<string>();
var worker2 = new List<string>();
var monitor = new List<string>();
Task[] subscribers = [Subscribe("%24share/workers/jobs", worker1), Subscribe("%24share/workers/jobs", worker2), Subscribe("jobs", monitor)];

// They are subscribed once each holds a ping; the group hands each ping to one worker. At most 20 s.
for (int tries = 0; Count(worker1) == 0 || Count(worker2) == 0 || Count(monitor) == 0; tries++)
{
    if (tries == 100)
    {
        Console.WriteLine("FAIL: not all subscribed within 20 s");
        return 1;
    }

    await server.Messages.PublishAsync("jobs", "ping\n"u8.ToArray(), false);
    await Task.Delay(200);
}

// The app publishes six jobs over mqtt://.
for (int n = 1; n <= 6; n++)
{
    CurlResponse published = await client.SendAsync(
        new CurlRequest(HttpMethod.Post, $"{server.BoundUrls[1]}jobs") { Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes($"job-{n}\n")) },
        CancellationToken.None);
    await published.Body.DisposeAsync();
}

// The test: within 10 s the workers hold six jobs between them, none twice, and each at least one; the monitor all six.
for (int tries = 0; (Jobs(worker1).Count + Jobs(worker2).Count < 6 || Jobs(monitor).Count < 6) && tries < 100; tries++)
{
    await Task.Delay(100);
}

stop.Cancel();
await Task.WhenAll(subscribers);
List<string> both = [.. Jobs(worker1), .. Jobs(worker2)];
Console.WriteLine($"monitor: {Jobs(monitor).Count} jobs");
bool pass = both.Count == 6 && both.Distinct().Count() == 6 && Jobs(worker1).Count > 0 && Jobs(worker2).Count > 0;
Console.WriteLine(pass ? "PASS: each job went to one worker, and both workers had some" : $"FAIL: {string.Join(" ", worker1)} / {string.Join(" ", worker2)}");
Console.WriteLine($"between them: {string.Join(" ", both.Order())}");

if (mode == "late")
{
    // After the run: a worker joining the group, and a plain subscriber to jobs, each for 3 s.
    using var late = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    var lateWorker = new List<string>();
    var latePlain = new List<string>();
    await Task.WhenAll(Subscribe("%24share/workers/jobs", lateWorker, late.Token), Subscribe("jobs", latePlain, late.Token));
    Console.WriteLine($"late worker: [{string.Join(" ", lateWorker)}]");
    Console.WriteLine($"late plain subscriber: [{string.Join(" ", latePlain)}]");
}

return pass ? 0 : 1;

// A subscription: Curl.Api's GET of an mqtt5:// URL subscribes to the topic filter in its path, and its body is
// each payload as it arrives, here a line each. It returns once the first payload comes, and ends when stopped.
async Task Subscribe(string filter, List<string> lines, CancellationToken? until = null)
{
    CancellationToken token = until ?? stop.Token;
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, mqtt5 + filter), token);
        await using Stream body = response.Body;
        using var reader = new StreamReader(body);
        while (await reader.ReadLineAsync(token) is string line)
        {
            lock (lines)
            {
                lines.Add(line);
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
}

int Count(List<string> lines)
{
    lock (lines)
    {
        return lines.Count;
    }
}

List<string> Jobs(List<string> lines)
{
    lock (lines)
    {
        return lines.Where(l => l.StartsWith("job-")).ToList();
    }
}
