// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test of a client behind an HTTP proxy: Surl.Api runs an origin and a proxy in one server,
// Curl.Api fetches through the proxy, and the recorded exchanges show the request passing through.
// Run: dotnet run run-a-proxy.cs [-- outside|tunnel-outside|allowed|every-origin]
//   (no argument) fetch hello.txt from the origin through the proxy
//   outside        ask the proxy for an origin outside loopback, 192.0.2.1
//   tunnel-outside the same through a CONNECT tunnel
//   allowed        the same from a proxy started with --proxy-allow-origin 192.0.2.1
//   every-origin   start a proxy with --proxy-allow-origin * and print its log
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "fetch";

// One server: the origin on the first listen URL, a proxy on the second.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    AdditionalArguments = { "--proxy", "http://127.0.0.1:0" },
    RecordExchanges = true,
    Log = log,
    LogLevel = LogLevel.Info,
};
if (which == "allowed")
{
    options.AdditionalArguments.Add("--proxy-allow-origin");
    options.AdditionalArguments.Add("192.0.2.1");
    options.AdditionalArguments.Add("--head-timeout");
    options.AdditionalArguments.Add("2");
}
else if (which == "every-origin")
{
    options.AdditionalArguments.Add("--proxy-allow-origin");
    options.AdditionalArguments.Add("*");
}

options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri origin = server.BoundUrls[0], proxy = server.BoundUrls[1];

if (which == "every-origin")
{
    Console.Write(log.ToString());
    return 0;
}

// The client under test, with the proxy set for every request, as curl -x sets it.
await using CurlClient client = CurlClient.Create(new CurlClientOptions
{
    Proxy = new CurlProxyOptions { Proxy = proxy.ToString(), HttpProxyTunnel = which == "tunnel-outside" },
});
string target = which == "fetch" ? $"{origin}hello.txt" : "http://192.0.2.1/hello.txt";
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, target), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    string via = response.Headers.FirstOrDefault(h => h.Key == "Via").Value ?? "none";
    Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase}, Via: {via}");
    if (response.StatusCode == 200)
    {
        Console.Write(text);
    }
}
catch (CurlException failure)
{
    Console.WriteLine($"CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
    return 1;
}

// What each listener received: the request line, and the Via header if any.
foreach (SurlExchangeRecord exchange in server.Exchanges)
{
    string[] lines = Encoding.ASCII.GetString(exchange.Received).Split("\r\n");
    string received = string.Join(" | ", lines.Where(l => l.StartsWith("Via:") || l.StartsWith("Proxy-Connection:")).Prepend(lines[0]));
    Console.WriteLine($"{(exchange.ListenUrl == proxy ? "proxy" : "origin")} received: {received}");
}

return 0;
