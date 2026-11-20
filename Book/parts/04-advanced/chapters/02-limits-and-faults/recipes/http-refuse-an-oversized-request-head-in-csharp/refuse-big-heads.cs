// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file with Surl.Api under a 1 KiB request-head limit and fetches it with Curl.Api,
// carrying a cookie of the size given. Run: dotnet run refuse-big-heads.cs -- <bytes> [default]
//   <bytes>   the cookie's length in bytes (1200 if not given)
//   default   leave surl's own limit in place instead of setting 1 KiB
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Surl.Api;

int cookieLength = args.Length > 0 ? int.Parse(args[0]) : 1200;
bool strict = !args.Contains("default");

// The server: one file in memory. SurlServerOptions has no property for this limit,
// so it goes in AdditionalArguments, as surl's own words.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
if (strict)
{
    options.AdditionalArguments.Add("--max-request-head");
    options.AdditionalArguments.Add("1k");
}

await using SurlServer server = await SurlServer.StartAsync(options);

// The client: one GET carrying a cookie of the chosen length.
await using CurlClient client = CurlClient.Create();
var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
{
    Headers = [new("Cookie", "session=" + new string('x', cookieLength))],
};
CurlResponse response = await client.SendAsync(request, CancellationToken.None);
Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase}");

// A 431 is an answer, not an exception: read what came with it.
await using Stream body = response.Body;
string text = await new StreamReader(body).ReadToEndAsync();
if (response.StatusCode == 200)
{
    Console.Write(text);
}
else
{
    string connection = response.Headers.FirstOrDefault(h => h.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)).Value ?? "(none)";
    Console.WriteLine($"body: {text.Length} bytes, Connection: {connection}");
}

// Like curl -f: 22 for an error status, 0 otherwise.
return response.StatusCode >= 400 ? 22 : 0;
