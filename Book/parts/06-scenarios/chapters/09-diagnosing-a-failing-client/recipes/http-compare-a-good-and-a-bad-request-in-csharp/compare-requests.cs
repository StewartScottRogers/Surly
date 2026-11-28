// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Two clients fetch one report from an in-process server; one is answered 200 and one 400. The test
// puts their two requests side by side, as the server recorded them, and prints the lines that differ.
// Run: dotnet run compare-requests.cs [-- no-host]
//   (no argument)  the bad client writes its header with a space before the colon
//   no-host        the bad client sends no Host header
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Output;

bool noHost = args.Length > 0 && args[0] == "no-host";

// The server records every exchange and keeps its verbose log in memory.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.Files["report.csv"] = "id,total\n1,40\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
string url = $"{server.BoundUrl}report.csv";

// Each client on a CurlClient of its own, so on a connection, and an exchange, of its own.
var good = new CurlRequest(HttpMethod.Get, url) { Headers = [new("X-Api-Key", "k123")] };
var bad = noHost
    ? new CurlRequest(HttpMethod.Get, url) { Headers = [new("X-Api-Key", "k123")], RemovedHeaders = ["Host"] }
    : new CurlRequest(HttpMethod.Get, url) { Headers = [new("X-Api-Key ", "k123")] };
foreach (var (name, request) in new[] { ("good", good), ("bad", bad) })
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    string connection = response.Headers.FirstOrDefault(h => h.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)).Value ?? "(none)";
    Console.WriteLine(response.StatusCode == 200
        ? $"{name} client: 200"
        : $"{name} client: {response.StatusCode}, body {text.TrimEnd('\n')}, Connection {connection}");
}

// The record is whole once every exchange that started has ended: wait for that, at most 10 s.
var started = new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted };
var ended = new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded };
for (var clock = System.Diagnostics.Stopwatch.StartNew(); server.FindEvents(ended).Count() < server.FindEvents(started).Count();)
{
    if (clock.Elapsed > TimeSpan.FromSeconds(10)) { Console.WriteLine("the exchanges did not end within 10 s"); return 1; }
    await Task.Delay(100);
}

// The exchange answered 200 and the one answered 400, by the status line the server sent.
SurlExchangeRecord Answered(string status) => server.Exchanges.First(e => Encoding.ASCII.GetString(e.Sent).StartsWith($"HTTP/1.1 {status} ", StringComparison.Ordinal));
string[] Lines(SurlExchangeRecord e) => Encoding.ASCII.GetString(e.Received).Split("\r\n").Where(l => l.Length > 0).ToArray();
SurlExchangeRecord ok = Answered("200"), refused = Answered("400");
string[] okLines = Lines(ok), refusedLines = Lines(refused);
Console.WriteLine($"good: exchange {ok.ExchangeId}, answered 200");
Console.WriteLine($"bad:  exchange {refused.ExchangeId}, answered 400");

// The lines only one of the two requests holds, then how many they share.
foreach (string line in okLines.Except(refusedLines)) Console.WriteLine($"- {line}");
foreach (string line in refusedLines.Except(okLines)) Console.WriteLine($"+ {line}");
Console.WriteLine($"same: {okLines.Intersect(refusedLines).Count()} lines");

// Why the server refused it, in its own words: its log lines for that exchange.
foreach (string line in log.ToString().Split('\n').Where(l => l.StartsWith($"#{refused.ExchangeId} * ", StringComparison.Ordinal) && l.Contains("answered 400")))
{
    Console.WriteLine($"why:  {line.TrimEnd('\r')[$"#{refused.ExchangeId} * ".Length..]}");
}
return 0;
