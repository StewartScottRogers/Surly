// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves one file from memory with Surl.Api and fetches it with Curl.Api, in one process.
// Run: dotnet run serve-and-fetch.cs [-- path]   (the path defaults to hello.txt)
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Surl.Api;

string path = args.Length > 0 ? args[0] : "hello.txt";

// The server: one in-memory file, on a free port of the loopback address.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
var server = await SurlServer.StartAsync(options);
Console.WriteLine($"listening on {server.BoundUrl}");

// The client: one GET to the address the server bound.
int status;
await using (CurlClient client = CurlClient.Create())
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    status = response.StatusCode;
    Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase}");
    await using Stream body = response.Body;
    if (status == 200)
    {
        Console.Write(await new StreamReader(body).ReadToEndAsync());
    }
}

// Stopping returns the exit code a surl process would have exited with.
Console.WriteLine($"server stopped: {await server.StopAsync()}");

// Like curl -f: 22 for an error status, 0 otherwise.
return status >= 400 ? 22 : 0;
