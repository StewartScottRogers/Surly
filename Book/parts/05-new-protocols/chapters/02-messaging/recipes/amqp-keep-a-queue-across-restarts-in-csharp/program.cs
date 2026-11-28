// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that a durable AMQP queue survives a restart: an in-process Surl server keeps its queues in
// DataDirectory, the test queues jobs, a worker takes one, the server is stopped and another starts on
// the same folder, and the worker takes what is left.
// Run: dotnet run program.cs -- <scenario>
//   restart   queue three jobs, take one, restart, take the next
//   scratch   the same restart, and the plain queue scratch
//   disk      the folder's files, while the server runs and after it stops
//   twice     a job queued after the first restart, then a second and a third restart
//   memory    the same test with no DataDirectory
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "restart";

// The folder the queues live in: it must exist before the server starts.
DirectoryInfo data = Directory.CreateTempSubdirectory("surl-queue-");
string lockFile = Path.Combine(data.FullName, ".surl", "lock");
string indexFile = Path.Combine(data.FullName, ".surl", "bus", "queues", "index");
await using CurlClient client = CurlClient.Create();

try
{
    SurlServer server = await Start(scenario != "memory");

    // Three jobs and one scratch message are put on the queues, then the worker takes job-1.
    foreach (string job in new[] { "job-1", "job-2", "job-3" })
    {
        await server.Messages.EnqueueAsync("jobs", Encoding.UTF8.GetBytes(job));
    }

    await server.Messages.EnqueueAsync("scratch", "tmp"u8.ToArray());
    Console.WriteLine($"worker took: {await Take(server, "jobs")}");

    switch (scenario)
    {
        case "disk":
            Console.WriteLine($"running: lock {File.Exists(lockFile)}, index {File.Exists(indexFile)}");
            await server.DisposeAsync();
            Console.WriteLine($"stopped: lock {File.Exists(lockFile)}, index {File.Exists(indexFile)}");
            break;
        case "memory":
            Console.WriteLine($"while it runs, jobs holds {await Count(server, "jobs")}");
            await server.DisposeAsync();
            server = await Start(false);
            Console.WriteLine($"after a restart, jobs holds {await Count(server, "jobs")}");
            await server.DisposeAsync();
            break;
        default:
            // The restart: stop the server, start another on the same folder.
            await server.DisposeAsync();
            server = await Start(true);
            Console.WriteLine($"after the restart: {await Take(server, "jobs")}");
            if (scenario == "scratch")
            {
                Console.WriteLine($"scratch after the restart: [{await Take(server, "scratch")}]");
            }

            if (scenario == "twice")
            {
                await server.Messages.EnqueueAsync("jobs", "job-4"u8.ToArray());
                await server.DisposeAsync();
                server = await Start(true);
                Console.WriteLine($"after the second restart: {await Take(server, "jobs")} then {await Take(server, "jobs")}");
                await server.DisposeAsync();
                server = await Start(true);
                Console.WriteLine($"after the third restart: [{await Take(server, "jobs")}]");
            }

            await server.DisposeAsync();
            break;
    }

    return 0;
}
finally
{
    data.Delete(true);
}

// The same options every time; the directory is what is kept. jobs is durable, scratch is not.
async Task<SurlServer> Start(bool keep)
{
    var options = new SurlServerOptions { ListenUrls = { "amqp://127.0.0.1:0" }, AllowAnonymous = true };
    options.Queues.Add("jobs;durable");
    options.Queues.Add("scratch");
    if (keep)
    {
        options.DataDirectory = data.FullName;
    }

    return await SurlServer.StartAsync(options);
}

// A GET takes one message and answers its body; an empty queue answers an empty body.
async Task<string> Take(SurlServer from, string queue)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{from.BoundUrl}%2F/{queue}"), CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body).ReadToEndAsync();
}

// Browsing counts what is ready without taking anything.
async Task<int> Count(SurlServer from, string queue) =>
    (await from.Messages.BrowseQueueAsync(queue, -1, 100, CancellationToken.None)).ReadyCount;
