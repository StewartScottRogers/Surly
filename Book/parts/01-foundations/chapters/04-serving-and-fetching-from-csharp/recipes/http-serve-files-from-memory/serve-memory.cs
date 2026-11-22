// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves text and binary files from memory with Surl.Api, nested paths included, and fetches them with Curl.Api.
// Run: dotnet run serve-memory.cs [-- path ...]   (the paths default to the three files below)
//      dotnet run serve-memory.cs -- --key <key>  (add a key of your own to Files and fetch it)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Surl.Api;

// The files: a key is the path the file is served at, a value its bytes.
byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0xFF, 0x00, 0xFF];
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["docs/guide/intro.txt"] = "nested two folders down\n"u8.ToArray();
options.Files["images/logo.png"] = png;

string[] paths = args.Length > 0 ? args : ["hello.txt", "docs/guide/intro.txt", "images/logo.png"];
if (paths[0] == "--key")
{
    options.Files[paths[1]] = "x\n"u8.ToArray();
    paths = [paths[1]];
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (ArgumentException misused)
{
    Console.WriteLine($"ArgumentException: {misused.Message}");
    return 2;
}

await using (server)
await using (CurlClient client = CurlClient.Create())
{
    foreach (string path in paths)
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
        await using Stream body = response.Body;
        using var bytes = new MemoryStream();
        await body.CopyToAsync(bytes);
        if (response.StatusCode != 200)
        {
            Console.WriteLine($"/{path}: {response.StatusCode} {response.ReasonPhrase}");
            continue;
        }

        // The bytes that came back, compared with the bytes given, and the type the server chose.
        string type = response.Headers.First(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value;
        string same = bytes.ToArray().AsSpan().SequenceEqual(options.Files[path]) ? "same bytes" : "different bytes";
        Console.WriteLine($"/{path}: {response.StatusCode} {type}, {bytes.Length} bytes, {same}");
    }
}

return 0;
