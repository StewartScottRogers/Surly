// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that proves a client went through its proxy: one SurlServer is the origin and the proxy,
// and every request the origin received is checked for the proxy's Via header.
// Run: dotnet run program.cs -- <scenario>
//   proxied  the client fetches hello.txt and prices.json through the proxy
//   bypass   it fetches hello.txt through the proxy, then once more without it
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;

string scenario = args[0];

// The origin is the listen URL; --proxy adds the proxy listener. RecordExchanges keeps what each received.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.AdditionalArguments.Add("--proxy");
options.AdditionalArguments.Add("http://127.0.0.1:0");
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["prices.json"] = "{\"widget\": 3}\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri origin = server.BoundUrls[0], proxy = server.BoundUrls[1];

// The client under test, set to use the proxy (curl -x), and the same client without it.
await using CurlClient viaProxy = CurlClient.Create(new CurlClientOptions { Proxy = new CurlProxyOptions { Proxy = proxy.ToString() } });
await using CurlClient direct = CurlClient.Create();

async Task Fetch(CurlClient client, string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{origin}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    byte[] bytes = new byte[64];
    int length = await body.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false);
    string via = response.Headers.FirstOrDefault(h => h.Key.Equals("Via", StringComparison.OrdinalIgnoreCase)).Value ?? "none";
    Console.WriteLine($"{path}: {response.StatusCode}, {length} bytes, Via: {via}");
}

if (scenario == "proxied")
{
    await Fetch(viaProxy, "hello.txt");
    await Fetch(viaProxy, "prices.json");
}
else
{
    await Fetch(viaProxy, "hello.txt");
    await Fetch(direct, "hello.txt");
}

// The test: every request head each listener received. One connection can carry several requests,
// so the heads are split at the blank line that ends each (GET has no body).
IEnumerable<string> Heads(Uri listener) => server.Exchanges
    .Where(e => e.ListenUrl == listener)
    .SelectMany(e => Encoding.ASCII.GetString(e.Received).Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries));

int forwarded = Heads(proxy).Count(h => h.StartsWith($"GET {origin}"));
var atOrigin = Heads(origin).ToList();
int proxied = atOrigin.Count(h => h.Contains("\r\nVia: 1.1 surl"));
Console.WriteLine($"proxy received {forwarded} requests for the origin");
Console.WriteLine($"origin: {proxied} via the proxy, {atOrigin.Count - proxied} direct");
return atOrigin.Count == proxied ? 0 : 1;
