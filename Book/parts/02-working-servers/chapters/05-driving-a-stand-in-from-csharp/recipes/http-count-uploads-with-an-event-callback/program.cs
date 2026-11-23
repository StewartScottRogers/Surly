// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test counts the uploads a client makes as they happen, with a callback on the server's store
// changes, and not by querying afterwards. The server and the client are both in this process.
// Run: dotnet run program.cs [-- <case>]
//   (none)      three uploads, counted by the callback
//   folder      the callback narrowed to /reports
//   refused     an upload the server refuses, then one it accepts
//   overwrite   the same path uploaded twice
//   test-put    a file the test puts itself
//   dispose     an upload after the callback is disposed
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Collections.Concurrent;
using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "count";

var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    AllowAnonymous = true,
    AllowUploads = true,
    MaxFileSize = which == "refused" ? 10 : null,
    RecordExchanges = true,
};
options.Files["reports/keep.txt"] = "already here\n"u8.ToArray();   // a client's upload needs its folder to exist
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The callback: for each content change it records what happened and releases the test's wait.
// It runs on its own thread, one event at a time, so it only appends and signals.
var seen = new ConcurrentQueue<string>();
var arrived = new SemaphoreSlim(0);
SurlEventQuery query = new() { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Content, Name = which == "folder" ? "/reports" : null };
IDisposable registration = server.OnEvent(query, (e, cancel) =>
{
    var change = (StoreChangedEvent)e;
    seen.Enqueue($"{change.Change} {change.Name} {change.ByteCount}");
    arrived.Release();
    return ValueTask.CompletedTask;
});

switch (which)
{
    case "count":
        await Upload("reports/a.txt", "alpha\n");
        await Upload("reports/b.txt", "beta\n");
        await Upload("notes.txt", "gamma\n");
        await Wait(3);
        break;

    case "folder":
        await Upload("reports/a.txt", "alpha\n");
        await Upload("notes.txt", "gamma\n");
        await Upload("reports/b.txt", "beta\n");
        await Wait(2);
        // Nothing more is due: look for one more, for a second, and expect none.
        Console.WriteLine($"a third within 1 s: {await arrived.WaitAsync(TimeSpan.FromSeconds(1))}");
        break;

    case "refused":
        Console.WriteLine($"too big: {await Upload("big.txt", "this is more than ten bytes\n")}");
        Console.WriteLine($"small: {await Upload("small.txt", "ok\n")}");
        await Wait(1);
        Console.WriteLine($"a second within 1 s: {await arrived.WaitAsync(TimeSpan.FromSeconds(1))}");
        break;

    case "overwrite":
        await Upload("a.txt", "first\n");
        await Upload("a.txt", "second, longer\n");
        await Wait(2);
        break;

    case "test-put":
        await server.Files.PutAsync("/seed.txt", "seeded\n"u8.ToArray());
        await Upload("a.txt", "alpha\n");
        await Wait(2);
        break;

    case "dispose":
        await Upload("a.txt", "alpha\n");
        await Wait(1);
        registration.Dispose();
        await Upload("b.txt", "beta\n");
        // The server has the second file; the callback was not told. A witness query on the log finds it.
        await server.WaitForEventAsync(new SurlEventQuery { Store = StoreKind.Content, Name = "/b.txt" }, TimeSpan.FromSeconds(30));
        break;
}

Console.WriteLine($"callback calls: {seen.Count}");
foreach (string line in seen)
{
    Console.WriteLine($"  {line}");
}
return 0;

// Wait for n calls of the callback, each for at most 30 seconds, so a missing one fails the test and does not hang it.
async Task Wait(int n)
{
    for (int i = 0; i < n; i++)
    {
        if (!await arrived.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException($"call {i + 1} of {n} never came");
        }
    }
}

async Task<int> Upload(string path, string text)
{
    var put = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{path}") { Body = Encoding.UTF8.GetBytes(text) };
    CurlResponse response = await client.SendAsync(put, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode;
}
