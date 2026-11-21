// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Reads one slice of a file with a Range header and checks what came back.
// Run: dotnet run read-slice.cs [-- first-last]   (the range defaults to 100-199)
// Exits 0 for the slice asked for, 2 when the range lies past the end, 1 for anything else.
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Surl.Api;

string range = args.Length > 0 ? args[0] : "100-199";

// The server: records.txt is 300 bytes, thirty lines of ten, so byte 100 starts "record 10".
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["records.txt"] = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Range(0, 30).Select(i => $"record {i:00}\n")));
await using var server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create();
var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}records.txt")
{
    Headers = [new("Range", $"bytes={range}")],
};
CurlResponse response = await client.SendAsync(request, CancellationToken.None);
string? contentRange = response.Headers.FirstOrDefault(h => h.Key.Equals("Content-Range", StringComparison.OrdinalIgnoreCase)).Value;
await using Stream body = response.Body;
byte[] bytes;
using (var buffer = new MemoryStream())
{
    await body.CopyToAsync(buffer);
    bytes = buffer.ToArray();
}

Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase}");
Console.WriteLine($"Content-Range: {contentRange ?? "(none)"}");
Console.WriteLine($"{bytes.Length} bytes");
switch (response.StatusCode)
{
    case 206 when contentRange?.StartsWith($"bytes {range}/", StringComparison.Ordinal) == true:
        Console.Write(Encoding.ASCII.GetString(bytes));
        return 0;
    case 416:
        // Content-Range: bytes */<length> is how the server says how long the file is.
        Console.WriteLine($"past the end: the file is {contentRange!.Split('/')[1]} bytes");
        return 2;
    default:
        Console.WriteLine("not the slice asked for");
        return 1;
}
