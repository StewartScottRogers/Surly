// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Prints the status, version and every header of a response, as curl -i does, then the body.
// Run: dotnet run show-headers.cs [-- path [method]]   (defaults: hello.txt GET)
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Surl.Api;

string path = args.Length > 0 ? args[0] : "hello.txt";
var method = new HttpMethod(args.Length > 1 ? args[1] : "GET");

// The server: one in-memory file, on a free port of the loopback address.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create();
CurlResponse response = await client.SendAsync(
    new CurlRequest(method, $"{server.BoundUrl}{path}"), CancellationToken.None);

// The head has arrived when SendAsync returns; the body has not been read yet.
Console.WriteLine($"HTTP/{response.Version} {response.StatusCode} {response.ReasonPhrase}");
foreach (KeyValuePair<string, string> header in response.Headers)
{
    Console.WriteLine($"{header.Key}: {header.Value}");
}

Console.WriteLine();
await using Stream body = response.Body;
string text = await new StreamReader(body).ReadToEndAsync();
Console.Write(text);
Console.WriteLine($"({System.Text.Encoding.UTF8.GetByteCount(text)} bytes of body)");
return 0;
