// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run program.cs`. Package versions are the book's pins in
// Book/build/packages.yml; build/pin.py rewrites these lines, and lint checks they match.
#:package Surl.Api@VERSION
#:package Curl.Api@VERSION

using Curl.Api;
using Surl.Api;

// The server, in-process: port 0 takes a free one, so no {{port}} and no servers: block.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The client.
await using CurlClient client = CurlClient.Create();
CurlResponse response = await client.SendAsync(
    new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
await using Stream body = response.Body;
Console.Write(await new StreamReader(body).ReadToEndAsync());

// Whatever the program prints and the exit code it returns are what recipe.yml checks.
return 0;
