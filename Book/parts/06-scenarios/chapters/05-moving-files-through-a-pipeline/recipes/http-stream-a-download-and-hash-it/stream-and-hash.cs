// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Streams a 16 MiB download to disk, hashing it as it arrives, and compares the hash with the
// SHA-256 of the file the server stores.
// Run: dotnet run stream-and-hash.cs -- <case>
//   whole    the server sends the whole file (the default)
//   dropped  the server drops the connection after 1,000,000 bytes
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Security.Cryptography;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "whole";

// The server: 16 MiB of seeded random bytes in memory, and a small file to warm up on.
byte[] original = new byte[16 * 1024 * 1024];
new Random(7).NextBytes(original);
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["warm-up.txt"] = "ready\n"u8.ToArray();
options.Files["big.bin"] = original;
await using var server = await SurlServer.StartAsync(options);

// The first request in a process sets the engine up, which takes a few seconds: get it out of the way.
await using (CurlClient warmClient = CurlClient.Create())
{
    CurlResponse warm = await warmClient.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}warm-up.txt"), CancellationToken.None);
    await using (warm.Body) { }
}

if (which == "dropped")
{
    // From now on every new connection is dropped once 1,000,000 bytes have been sent on it.
    server.Faults.Add(SurlFault.DropConnection() with { AfterBytes = 1_000_000 });
}

// The download: each block read from the body goes to the file and into the hash, so the whole file is never in memory.
// Timeout bounds the whole transfer, body included: a break the client is never told of still ends, as a CurlException.
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(15) });
long heapBefore = GC.GetTotalMemory(true);
using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
long received = 0;
string? failure = null;
CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}big.bin"), CancellationToken.None);
await using (Stream body = response.Body)
await using (FileStream file = File.Create("big.bin.part"))
{
    byte[] block = new byte[81920];
    try
    {
        int read;
        while ((read = await body.ReadAsync(block)) > 0)
        {
            hash.AppendData(block, 0, read);
            await file.WriteAsync(block.AsMemory(0, read));
            received += read;
        }
    }
    catch (CurlException e)
    {
        failure = e.GetType().Name;
    }
}

long heapGrowth = GC.GetTotalMemory(true) - heapBefore;
string streamed = Convert.ToHexStringLower(hash.GetHashAndReset());

// The server's own copy, hashed for comparison.
SurlFileContents stored = await server.Files.ReadAsync("/big.bin");
string expected = Convert.ToHexStringLower(SHA256.HashData(stored.Bytes.Span));
Console.WriteLine($"status: {response.StatusCode}");
Console.WriteLine(failure is null ? $"received: {received} bytes" : $"failed: {failure} after fewer than {stored.ByteCount} bytes: {(received < stored.ByteCount ? "yes" : "no")}");
Console.WriteLine($"same SHA-256 as the server's copy: {(streamed == expected ? "yes" : "no")}");
Console.WriteLine($"heap grew by less than 4 MiB: {(heapGrowth < 4 * 1024 * 1024 ? "yes" : "no")}");

// Keep the file only when it matches; a partial or wrong download is removed, never left to be trusted.
if (failure is null && streamed == expected)
{
    File.Move("big.bin.part", "big.bin", overwrite: true);
    Console.WriteLine("kept as big.bin");
    return 0;
}

File.Delete("big.bin.part");
Console.WriteLine("removed big.bin.part");
return 1;
