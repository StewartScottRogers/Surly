// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Copies a 64 MiB file served by Surl.Api straight to disk from Curl.Api's body stream, and
// checks the copy: its length, its SHA-256, and how much memory the program held while copying.
// Run: dotnet run stream-to-disk.cs [-- buffer]
//   (no argument) stream the body to a file, a buffer at a time
//   buffer        read the whole body into memory first, then write it
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Security.Cryptography;
using Curl.Api;
using Surl.Api;

const long Size = 64L * 1024 * 1024;
const long Mebibyte = 1024 * 1024;
bool buffered = args is ["buffer"];

// The large file, written to disk a block at a time so it is never whole in memory either.
Directory.CreateDirectory("served");
byte[] block = new byte[Mebibyte];
using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
await using (FileStream source = File.Create("served/large.bin"))
{
    var random = new Random(1);
    for (long done = 0; done < Size; done += block.Length)
    {
        random.NextBytes(block);
        hash.AppendData(block);
        await source.WriteAsync(block);
    }

    Console.WriteLine($"served: {Size} bytes, sha256 {Convert.ToHexStringLower(hash.GetHashAndReset())[..16]}");
}

// The server serves the folder from disk.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, DataDirectory = "served" };
await using var server = await SurlServer.StartAsync(options);

long most = 0;
void Measure() => most = Math.Max(most, GC.GetTotalMemory(forceFullCollection: true));
Measure();
long before = most;

await using (CurlClient client = CurlClient.Create())
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}large.bin"), CancellationToken.None);
    Console.WriteLine($"{response.StatusCode}, Content-Length {response.Headers.First(h => h.Key == "Content-Length").Value}");
    await using Stream body = response.Body;
    await using FileStream file = File.Create("large.bin");
    if (buffered)
    {
        // The way to avoid: the whole body in memory before a byte reaches the disk.
        var whole = new MemoryStream();
        await body.CopyToAsync(whole);
        Measure();
        whole.Position = 0;
        await whole.CopyToAsync(file);
    }
    else
    {
        // Straight to disk: one buffer, refilled from the body as the transfer delivers it.
        byte[] buffer = new byte[81920];
        long copied = 0, next = 0;
        int read;
        while ((read = await body.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            copied += read;
            if (copied >= next)
            {
                Measure();
                next += 8 * Mebibyte;
            }
        }
    }
}

using (FileStream copy = File.OpenRead("large.bin"))
{
    Console.WriteLine($"written: {copy.Length} bytes, sha256 {Convert.ToHexStringLower(await SHA256.HashDataAsync(copy))[..16]}");
}

long held = (most - before) / Mebibyte;
Console.WriteLine($"most memory held while copying: {(held < 16 ? "under 16 MiB" : "16 MiB or more")}");
Console.Error.WriteLine($"held {held} MiB");
return 0;
