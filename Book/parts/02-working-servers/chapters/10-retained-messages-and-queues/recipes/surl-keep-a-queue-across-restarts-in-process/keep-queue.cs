// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A durable queue across a restart, in process: a Surl server with a data directory and the queue
// jobs;durable is seeded with three jobs, stopped, started again on the same folder, and the queue is
// browsed to see what came back.
// Run: dotnet run keep-queue.cs [-- <case>]
//   (no argument)  three jobs enqueued, a restart, then a browse of jobs
//   scratch        the same restart for a queue declared without ;durable
//   nodir          jobs;durable with no DataDirectory
//   disk           the files the queue is kept in
//   unknown        EnqueueAsync to a queue the server was not started with
//   inuse          a second server on the folder while the first runs
#:package Surly.Surl.Api@0.1.13

using System.Text;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "durable";
string queue = mode == "scratch" ? "scratch" : "jobs";
string folder = Path.Combine(Path.GetTempPath(), "keep-queue-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    // An MQTT broker whose bus is kept in folder, with the durable queue jobs and the plain queue scratch.
    SurlServerOptions Options() => new()
    {
        ListenUrls = { "mqtt://127.0.0.1:0" },
        DataDirectory = mode == "nodir" ? null : folder,
        Queues = { "jobs;durable", "scratch" },
    };

    await using (SurlServer first = await SurlServer.StartAsync(Options()))
    {
        try
        {
            foreach (string job in new[] { "resize photo 1", "resize photo 2", "resize photo 3" })
            {
                await first.Messages.EnqueueAsync(mode == "unknown" ? "job" : queue, Encoding.UTF8.GetBytes(job), CancellationToken.None);
            }
        }
        catch (SurlSeedRefusedException e)
        {
            Console.WriteLine($"refused: {e.Message}");
            return 1;
        }

        Console.WriteLine($"before the restart, {queue} holds {(await first.Messages.BrowseQueueAsync(queue, -1, 100, CancellationToken.None)).ReadyCount}");
        if (mode == "inuse")
        {
            try
            {
                await using SurlServer second = await SurlServer.StartAsync(Options());
            }
            catch (SurlServerStartException e)
            {
                Console.WriteLine($"second server refused, exit {(int)e.ExitCode}: {e.Lines[0].Replace(folder, "<folder>")}");
                return 1;
            }
        }
    }

    if (mode == "disk")
    {
        // Relative paths, sorted, with / between folders on every platform.
        foreach (string file in Directory.GetFiles(Path.Combine(folder, ".surl", "bus", "queues"), "*", SearchOption.AllDirectories).Order())
        {
            Console.WriteLine(Path.GetRelativePath(folder, file).Replace('\\', '/'));
        }

        return 0;
    }

    // The restart: a new server on the same folder, with the same queues.
    await using (SurlServer again = await SurlServer.StartAsync(Options()))
    {
        SurlQueuePage page = await again.Messages.BrowseQueueAsync(queue, -1, 100, CancellationToken.None);
        Console.WriteLine($"after the restart, {queue} holds {page.ReadyCount}");
        foreach (SurlQueueMessage message in page.Messages)
        {
            Console.WriteLine($"{message.Position}: {Encoding.UTF8.GetString(message.Payload.Span)}");
        }
    }

    return 0;
}
finally
{
    // Only after every server on it is stopped: a server holds the folder's lock file while it runs.
    Directory.Delete(folder, recursive: true);
}
