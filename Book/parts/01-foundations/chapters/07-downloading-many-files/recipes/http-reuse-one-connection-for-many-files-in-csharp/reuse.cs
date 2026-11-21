// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches three files one after another from a Surl.Api server that records its exchanges, and
// counts the connections: Curl.Api's NumConnects per request, and the exchanges the server saw.
// Run: dotnet run reuse.cs [-- <case>]
//   shared      one CurlClient for all three requests (the default)
//   fresh       a new CurlClient for each request
//   close       one client, each request sending Connection: close
//   http1.0     one client, each request over HTTP/1.0
//   pause       one client, 2 s between requests
//   idle        one client, 2 s between requests, against a server with a 1 s idle timeout
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Options.Http;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "shared";

// The server: three small files in memory, every exchange recorded. For HTTP an exchange is one connection.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
if (mode == "idle")
{
    options.IdleTimeout = TimeSpan.FromSeconds(1);
}

string[] names = ["a.txt", "b.txt", "c.txt"];
foreach (string name in names)
{
    options.Files[name] = System.Text.Encoding.UTF8.GetBytes($"file {name[0]}\n");
}

await using var server = await SurlServer.StartAsync(options);

// The first Curl.Api request in a process pays a one-time set-up; pay it on a client of its own.
await using (CurlClient warmUp = CurlClient.Create())
{
    CurlResponse first = await warmUp.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}a.txt"), CancellationToken.None);
    await first.Body.DisposeAsync();
}

int before = server.Exchanges.Count;

// The client: one, shared by every request, unless the case makes a fresh one each time.
CurlClient client = CurlClient.Create();
foreach (string name in names)
{
    if (mode == "fresh" && name != names[0])
    {
        await client.DisposeAsync();
        client = CurlClient.Create();
    }

    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{name}");
    request = mode switch
    {
        "close" => request with { Headers = [new("Connection", "close")] },
        "http1.0" => request with { Http = new CurlHttpOptions { HttpVersion = HttpVersion.Http10 } },
        _ => request,
    };

    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using (Stream body = response.Body)
    {
        await body.CopyToAsync(Stream.Null);
    }

    // NumConnects: 1 when this request opened a connection, 0 when it reused one.
    Console.WriteLine($"{name}: {response.StatusCode}, NumConnects {response.Info.NumConnects}");
    if (mode is "pause" or "idle" && name != names[^1])
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}

await client.DisposeAsync();
Console.WriteLine($"exchanges the server saw: {server.Exchanges.Count - before}");
return 0;
