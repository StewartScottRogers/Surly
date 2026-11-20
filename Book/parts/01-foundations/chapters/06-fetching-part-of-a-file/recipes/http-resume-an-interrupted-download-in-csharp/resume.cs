// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Resumes a partial download from the length of the file it left, and checks the joined file.
// Run: dotnet run resume.cs -- [mode [file]]
//   mode   resume (the default): a Range request from the end of the file
//          if-range: the same, with If-Range set to the Last-Modified date of a HEAD request
//          if-range-old: the same, with If-Range set to an old date, as if the file had changed since
//   file   the partial file (dl/records.txt by default); the program writes it back in full
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "resume";
string path = args.Length > 1 ? args[1] : "dl/records.txt";

// The original: 300 bytes, thirty lines of ten, served from memory on a free port.
byte[] original = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 30).Select(i => $"record {i:00}\n")));
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["records.txt"] = original;
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();
string url = $"{server.BoundUrl}records.txt";

// The offset is the length of what is already on disk.
long have = File.Exists(path) ? new FileInfo(path).Length : 0;
Console.WriteLine($"partial: {have} bytes");

var headers = new List<KeyValuePair<string, string>>();
if (have > 0)
{
    headers.Add(new("Range", $"bytes={have}-"));
}

if (mode == "if-range")
{
    // The date a first download would have saved: the file's Last-Modified.
    CurlResponse head = await client.SendAsync(new CurlRequest(HttpMethod.Head, url), CancellationToken.None);
    headers.Add(new("If-Range", head.Headers.First(h => h.Key.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase)).Value));
}
else if (mode == "if-range-old")
{
    headers.Add(new("If-Range", "Wed, 01 Jan 2020 00:00:00 GMT"));
}

CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url) { Headers = headers }, CancellationToken.None);
string? contentRange = response.Headers.FirstOrDefault(h => h.Key.Equals("Content-Range", StringComparison.OrdinalIgnoreCase)).Value;
await using Stream body = response.Body;
byte[] received;
using (var buffer = new MemoryStream())
{
    await body.CopyToAsync(buffer);
    received = buffer.ToArray();
}

if (response.StatusCode == 206 && contentRange?.StartsWith($"bytes {have}-", StringComparison.Ordinal) == true)
{
    // The missing bytes: append them to what is there.
    await using (var file = new FileStream(path, FileMode.Append))
    {
        await file.WriteAsync(received);
    }

    Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase} {contentRange}");
    Console.WriteLine($"appended {received.Length} bytes");
}
else if (response.StatusCode == 416)
{
    // Content-Range: bytes */<length> says the file is as long as what is on disk: nothing is missing.
    Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase} {contentRange}");
    Console.WriteLine(contentRange == $"bytes */{have}" ? "already complete" : "longer than the original");
}
else
{
    // 200: the whole file, because there was no Range or the server would not honour it. Start over.
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    await File.WriteAllBytesAsync(path, received);
    Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase}");
    Console.WriteLine($"rewrote {received.Length} bytes");
}

// The check: the file on disk against what the server holds.
byte[] joined = await File.ReadAllBytesAsync(path);
bool same = joined.AsSpan().SequenceEqual(original);
Console.WriteLine($"file: {joined.Length} bytes, identical to the original: {(same ? "yes" : "no")}");
return same ? 0 : 1;
