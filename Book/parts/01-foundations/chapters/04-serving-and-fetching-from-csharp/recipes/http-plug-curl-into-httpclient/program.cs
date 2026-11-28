// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Runs the same HttpClient code twice, once on .NET's own handler and once on Curl's engine through
// CurlHttpMessageHandler, and compares what each got. Run: dotnet run program.cs [-- <case>]
//   compare    GET hello.txt both ways (the default)
//   missing    GET a path the server does not have, then EnsureSuccessStatusCode
//   requests   what the server received from each handler, header names only
//   reuse      three GETs on one HttpClient: how many connections each handler opened
//   timeout    GET a path the server holds for 5 s, with HttpClient.Timeout at 1 s
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Diagnostics;
using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "compare";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["slow.txt"] = "too late\n"u8.ToArray();
options.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(5)) with { Stage = FaultStage.Command, Path = "/slow.txt" });
await using SurlServer server = await SurlServer.StartAsync(options);

// The code under test, written against HttpClient only: it knows nothing of the handler underneath.
async Task<string> FetchAsync(HttpClient http, string path)
{
    using HttpResponseMessage response = await http.GetAsync(path);
    string body = await response.Content.ReadAsStringAsync();
    if (which == "missing")
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            return $"{(int)response.StatusCode} {response.ReasonPhrase}, EnsureSuccessStatusCode threw HttpRequestException {(int?)e.StatusCode}";
        }
    }

    return $"{(int)response.StatusCode} {response.ReasonPhrase}, HTTP/{response.Version}, {response.Content.Headers.ContentType}, {response.Content.Headers.ContentLength} bytes, {body.TrimEnd()}";
}

// The first Curl.Api request in a process sets the engine up, which takes a few seconds: on a
// client of its own, so it shares no connection with what is compared.
await using (CurlClient warm = CurlClient.Create())
{
    CurlResponse ready = await warm.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
    await using (ready.Body) { }
}

await using CurlClient curl = CurlClient.Create();
var results = new List<string>();
foreach (string name in new[] { "sockets", "curl" })
{
    // The only line that differs: which handler the HttpClient sends through.
    HttpMessageHandler handler = name == "curl" ? new CurlHttpMessageHandler(curl) : new SocketsHttpHandler();
    TimeSpan timeout = TimeSpan.FromSeconds(which == "timeout" ? 1 : 30);
    using var http = new HttpClient(handler) { BaseAddress = server.BoundUrl, Timeout = timeout };
    int before = server.Exchanges.Count;
    if (which == "timeout")
    {
        var clock = Stopwatch.StartNew();
        try
        {
            await FetchAsync(http, "slow.txt");
            Console.WriteLine($"{name}: answered");
        }
        catch (TaskCanceledException e)
        {
            string within = clock.Elapsed.TotalSeconds is >= 0.9 and < 4 ? "1 to 4 s" : $"{clock.Elapsed.TotalSeconds:F1} s";
            Console.WriteLine($"{name}: {e.GetType().Name} after {within}, inner {e.InnerException?.GetType().Name ?? "none"}");
        }

        continue;
    }

    string path = which == "missing" ? "missing.txt" : "hello.txt";
    int times = which == "reuse" ? 3 : 1;
    string result = "";
    for (int i = 0; i < times; i++)
    {
        result = await FetchAsync(http, path);
    }

    results.Add(result);
    switch (which)
    {
        case "requests":
            // The header names of the request head the server received, in the order sent. The record can
            // trail the client by a moment, so read it again until it holds a whole head, for up to 10 s.
            string received = Encoding.ASCII.GetString(server.Exchanges[^1].Received);
            for (var waited = Stopwatch.StartNew(); !received.Contains("\r\n\r\n") && waited.Elapsed.TotalSeconds < 10;)
            {
                await Task.Delay(100);
                received = Encoding.ASCII.GetString(server.Exchanges[^1].Received);
            }

            string head = received.Split("\r\n\r\n")[0];
            string[] lines = head.Split("\r\n");
            Console.WriteLine($"{name}: {lines[0]} | {string.Join(", ", lines.Skip(1).Select(l => l[..l.IndexOf(':')]))}");
            break;
        case "reuse":
            Console.WriteLine($"{name}: {times} requests, {server.Exchanges.Count - before} connection(s)");
            break;
        default:
            Console.WriteLine($"{name}: {result}");
            break;
    }
}

if (which is "compare" or "missing")
{
    Console.WriteLine($"same answer: {(results[0] == results[1] ? "yes" : "no")}");
}

return 0;
