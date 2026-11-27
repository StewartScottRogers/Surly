// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Starts an in-process server on port 0 and builds every client URL from the URLs it bound.
// Run: dotnet run bound-urls.cs [-- listen-url ...]   (default: http://127.0.0.1:0 http://localhost:0)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Surl.Api;

string[] listen = args.Length > 0 ? args : ["http://127.0.0.1:0", "http://localhost:0"];

var options = new SurlServerOptions();
foreach (string url in listen)
{
    options.ListenUrls.Add(url);
}

options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    // surl would have refused this start: the same exit code and message, and nothing bound.
    Console.WriteLine($"refused: {(int)refused.ExitCode} ({refused.ExitCode})");
    Console.WriteLine(refused.Message);
    return (int)refused.ExitCode;
}

await using (server)
{
    // BoundUrls: one per listen URL, in order, port 0 replaced by the port bound. BoundUrl is the first.
    Console.WriteLine($"ListenUrls: {string.Join(" ", options.ListenUrls)}");
    Console.WriteLine($"BoundUrls:  {string.Join(" ", server.BoundUrls)}");
    Console.WriteLine($"BoundUrl:   {server.BoundUrl}");

    await using CurlClient client = CurlClient.Create();
    foreach (Uri bound in server.BoundUrls)
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{bound}hello.txt"), CancellationToken.None);
        await using Stream body = response.Body;
        Console.WriteLine($"{bound}hello.txt: {response.StatusCode} {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
    }
}

return 0;
