// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves fifty files from a server that takes eight connections per address, fetches them all
// at once with one CurlClient, saves them under out/ and checks every saved file is whole.
// Run: dotnet run parallel.cs -- <case>
//   capped    MaxConnectionsPerHost 8, the server's limit (the default)
//   uncapped  no cap on the client
//   retry     no cap, and each 503 sent again a second later, up to ten times
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Security.Cryptography;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "capped";
int cap = which == "capped" ? 8 : 0;
string folder = $"out-{which}";
Directory.CreateDirectory(folder);

// The server: fifty files of 100,000 bytes in memory, and at most eight connections from one address.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, MaxConnectionsPerAddress = 8 };
options.Files["warm-up.txt"] = "ready\n"u8.ToArray();
for (int i = 1; i <= 50; i++)
{
    byte[] bytes = new byte[100_000];
    new Random(i).NextBytes(bytes);
    options.Files[$"f{i}.bin"] = bytes;
}

await using var server = await SurlServer.StartAsync(options);

// The client: MaxConnectionsPerHost is the most connections its concurrent requests may hold to one host.
await using CurlClient client = CurlClient.Create(new CurlClientOptions { MaxConnectionsPerHost = cap });

// The first request in a process sets the engine up, which takes a few seconds: get it out of the way.
CurlResponse warm = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}warm-up.txt"), CancellationToken.None);
await using (warm.Body) { }

// All fifty at once: each task saves one file, a 200's body only, and answers its last status.
int exceptions = 0;
int retried = 0;
int[] statuses = await Task.WhenAll(Enumerable.Range(1, 50).Select(async i =>
{
    for (int attempt = 0; ; attempt++)
    {
        try
        {
            CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}f{i}.bin"), CancellationToken.None);
            await using Stream body = response.Body;
            if (response.StatusCode == 503 && which == "retry" && attempt < 10)
            {
                Interlocked.Increment(ref retried);
                await Task.Delay(TimeSpan.FromSeconds(1));
                continue;
            }

            if (response.StatusCode == 200)
            {
                await using FileStream file = File.Create(Path.Combine(folder, $"f{i}.bin"));
                await body.CopyToAsync(file);
            }

            return response.StatusCode;
        }
        catch (Exception)
        {
            Interlocked.Increment(ref exceptions);
            return 0;
        }
    }
}));

// The check: each saved file has the SHA-256 of the file the server holds.
int whole = Enumerable.Range(1, 50).Count(i =>
{
    string path = Path.Combine(folder, $"f{i}.bin");
    return File.Exists(path) && SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(SHA256.HashData(options.Files[$"f{i}.bin"]));
});

Console.WriteLine($"server limit per address: 8, client cap: {(cap == 0 ? "none" : cap)}{(which == "retry" ? ", 503 retried" : "")}");
Console.WriteLine($"last status 200: {statuses.Count(s => s == 200)}, 503 Service Unavailable: {statuses.Count(s => s == 503)}, exceptions: {exceptions}");
Console.WriteLine($"saved under {folder} and whole: {whole} of 50");
if (which == "retry") Console.WriteLine($"sent again after a 503: {retried}");

// Like curl -f: 22 unless every file arrived whole.
return whole == 50 ? 0 : 22;
