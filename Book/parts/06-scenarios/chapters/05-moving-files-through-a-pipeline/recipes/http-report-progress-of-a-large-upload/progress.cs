// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Uploads 16 MiB with Curl.Api, records every progress report, and checks the reports climb to the
// size the server stored.
// Run: dotnet run progress.cs -- <case>
//   upload   to a server that takes uploads (the default)
//   refused  to a server that does not (no AllowUploads)
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Security.Cryptography;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "upload";

// The server: anyone may log in, and uploads are taken only in the upload case. Both loosen surl's defaults.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    AllowAnonymous = true,
    AllowUploads = which == "upload",
};
options.Files["warm-up.txt"] = "ready\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The first request in a process sets the engine up, which takes a few seconds: get it out of the way.
CurlResponse warm = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}warm-up.txt"), CancellationToken.None);
await using (warm.Body) { }

// 16 MiB to upload, from a stream; Progress is told about the transfer as it goes.
byte[] data = new byte[16 * 1024 * 1024];
new Random(1).NextBytes(data);
var reports = new ProgressLog();
CurlResponse put = await client.SendAsync(
    new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}upload.bin") { Body = CurlContent.FromStream(new MemoryStream(data), data.Length), Progress = reports },
    CancellationToken.None);
await using (put.Body) { }
Console.WriteLine($"PUT: {put.StatusCode} {put.ReasonPhrase}");

// What the reports say.
List<CurlTransferProgress> all = reports.All;
CurlTransferProgress last = all[^1];
bool climbs = all.Zip(all.Skip(1)).All(pair => pair.Second.UploadNow >= pair.First.UploadNow);
Console.WriteLine($"reports: {(all.Count >= 10 ? "10 or more" : all.Count.ToString())}, never going back: {(climbs ? "yes" : "no")}");
Console.WriteLine($"last report: {last.UploadNow} of {last.UploadTotal} bytes");
Console.WriteLine($"phases seen: {string.Join(", ", all.Select(p => p.Phase).Distinct())}");

// What the server stored.
if (which == "upload")
{
    SurlFileContents stored = await server.Files.ReadAsync("/upload.bin");
    Console.WriteLine($"server stored: {stored.ByteCount} bytes, same SHA-256: {(SHA256.HashData(stored.Bytes.Span).SequenceEqual(SHA256.HashData(data)) ? "yes" : "no")}");
}

return put.StatusCode is 201 or 204 ? 0 : 1;

// Keeps every report, in the order the engine made them. Report runs on the engine's own thread.
sealed class ProgressLog : IProgress<CurlTransferProgress>
{
    private readonly List<CurlTransferProgress> reports = new();

    public List<CurlTransferProgress> All
    {
        get { lock (reports) { return reports.ToList(); } }
    }

    public void Report(CurlTransferProgress value)
    {
        lock (reports)
        {
            reports.Add(value);
        }
    }
}
