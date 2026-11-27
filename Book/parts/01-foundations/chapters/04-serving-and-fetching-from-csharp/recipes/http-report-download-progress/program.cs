// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Downloads 16 MiB with Curl.Api and prints a progress line each time another quarter has arrived.
// Run: dotnet run program.cs [-- <path>]
//   big.bin      16 MiB (the default)
//   missing.bin  a path the server does not have
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Surl.Api;

string path = args.Length > 0 ? args[0] : "big.bin";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["big.bin"] = new byte[16 * 1024 * 1024];
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(60) });

// The first request in a process sets the engine up, which takes a few seconds: get it out of the way.
CurlResponse warm = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
await using (warm.Body) { }

// Progress is told about the transfer as it goes; QuarterLine prints a line per quarter as it is passed.
var progress = new QuarterLine();
CurlResponse response = await client.SendAsync(
    new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}") { Progress = progress }, CancellationToken.None);
await using (Stream body = response.Body)
{
    await body.CopyToAsync(Stream.Null);
}

Console.WriteLine("body read to its end");

Console.WriteLine($"{path}: {response.StatusCode} {response.ReasonPhrase}");
Console.WriteLine($"reports: {(progress.Count >= 10 ? "10 or more" : progress.Count.ToString())}, never going back: {(progress.NeverWentBack ? "yes" : "no")}");
Console.WriteLine($"last report: {progress.Last.DownloadNow} of {progress.Last.DownloadTotal} bytes");
Console.WriteLine($"phases seen: {string.Join(", ", progress.Phases)}");
return 0;

// Prints "downloaded N% (now of total bytes)" when a report passes 25, 50, 75 and 100 percent.
// Report runs on the engine's own thread, one report at a time, in order.
sealed class QuarterLine : IProgress<CurlTransferProgress>
{
    private int nextQuarter = 1;

    public int Count { get; private set; }

    public bool NeverWentBack { get; private set; } = true;

    public CurlTransferProgress Last { get; private set; }

    public List<CurlTransferPhase> Phases { get; } = new();

    public void Report(CurlTransferProgress value)
    {
        Count++;
        NeverWentBack &= Count == 1 || value.DownloadNow >= Last.DownloadNow;
        Last = value;
        if (!Phases.Contains(value.Phase))
        {
            Phases.Add(value.Phase);
        }

        // A total of 0 means the size is not known (yet): no percentage to give.
        while (value.DownloadTotal > 0 && nextQuarter <= 4 && value.DownloadNow * 4 >= value.DownloadTotal * nextQuarter)
        {
            Console.WriteLine($"downloaded {nextQuarter * 25}% ({value.DownloadNow} of {value.DownloadTotal} bytes)");
            nextQuarter++;
        }
    }
}
