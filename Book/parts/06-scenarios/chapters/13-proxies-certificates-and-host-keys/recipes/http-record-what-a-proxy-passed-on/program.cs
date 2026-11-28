// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A proxy and an origin in one in-process server. The client sends four requests through the
// proxy; the test reads both listeners' recorded exchanges and proves which requests the proxy
// forwarded and which it refused.
// Run: dotnet run program.cs -- <free port> [<case>]
//   free port  a loopback port with nothing listening on it
//   (no case)  what the proxy did with each request, and what reached the origin
//   counts     the requests each listener recorded
//   reasons    the proxy's own account of each request, from its verbose log
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Output;

string freePort = args[0];
string which = args.Length > 1 ? args[1] : "record";

// The origin (BoundUrls[0]) and, through AdditionalArguments, a proxy (BoundUrls[1]). RecordExchanges keeps the bytes.
// Log at Verbose is surl -v: the proxy writes what it did with each request.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true, Log = TextWriter.Synchronized(log), LogLevel = LogLevel.Verbose };
options.AdditionalArguments.Add("--proxy");
options.AdditionalArguments.Add("http://127.0.0.1:0");
options.Files["orders.csv"] = "id,total\n1,40\n"u8.ToArray();
options.Files["stock.csv"] = "sku,count\nA1,4\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
Uri origin = server.BoundUrls[0], proxy = server.BoundUrls[1];

// The client: every request through the proxy. Two go to the origin; one to an address outside
// loopback; one to a loopback port where nothing listens.
string[] targets = [$"{origin}orders.csv", $"{origin}stock.csv", "http://192.0.2.1/orders.csv", $"http://127.0.0.1:{freePort}/orders.csv"];
await using (CurlClient client = CurlClient.Create(new CurlClientOptions { Proxy = new CurlProxyOptions { Proxy = proxy.ToString() } }))
{
    foreach (string target in targets)
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, target), CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        Console.WriteLine($"client: {Label(target)} {response.StatusCode}");
    }
}

// Every request head each listener received, with the status of the answer that followed it.
List<(string Head, string Status)> Requests(Uri listener)
{
    var found = new List<(string, string)>();
    foreach (SurlExchangeRecord exchange in server.Exchanges.Where(e => e.ListenUrl == listener))
    {
        string? head = null;
        foreach (SurlExchangeChunk chunk in exchange.Chunks)
        {
            string text = Encoding.ASCII.GetString(chunk.Bytes.Span);
            if (chunk.Direction == SurlExchangeDirection.Received && text.StartsWith("GET ", StringComparison.Ordinal))
            {
                head = text;
            }
            else if (chunk.Direction == SurlExchangeDirection.Sent && head is not null && text.StartsWith("HTTP/1.1 ", StringComparison.Ordinal))
            {
                found.Add((head, text.Split(' ')[1]));
                head = null;
            }
        }
    }
    return found;
}
var atProxy = Requests(proxy);
var atOrigin = Requests(origin);

if (which == "reasons")
{
    await server.StopAsync();
    foreach (string line in log.ToString().Split('\n').Where(l => l.Contains("* Forwarded ") || l.Contains("* Answered ")))
    {
        Console.WriteLine($"log: {Label(line[(line.IndexOf("* ") + 2)..].TrimEnd()).Replace(origin.Authority, "origin")}");
    }
    return 0;
}

if (which == "counts")
{
    Console.WriteLine($"proxy listener: {atProxy.Count} requests; origin: {atOrigin.Count} requests, {atOrigin.Count(r => r.Head.Contains("\r\nVia: 1.1 surl\r\n"))} with Via: 1.1 surl");
    return 0;
}

// The proof: a request the proxy forwarded reached the origin, in origin form, with Via: 1.1 surl.
// A request it refused got its answer from the proxy and never reached the origin.
foreach ((string head, string status) in atProxy)
{
    string target = head.Split(' ')[1];                    // absolute form: http://host:port/path
    string path = new Uri(target).AbsolutePath;
    bool reached = new Uri(target).Authority == origin.Authority
        && atOrigin.Any(r => r.Head.StartsWith($"GET {path} HTTP/1.1\r\n") && r.Head.Contains("\r\nVia: 1.1 surl\r\n"));
    Console.WriteLine($"proxy: {Label(target)} answered {status}; {(reached ? "forwarded: the origin received it with Via" : "not passed on: the origin never received it")}");
}
return 0;

string Label(string text) => text.Replace(origin.ToString(), "origin/").Replace($"http://127.0.0.1:{freePort}/", "nothing-listening/").Replace($"127.0.0.1:{freePort}", "nothing-listening");
