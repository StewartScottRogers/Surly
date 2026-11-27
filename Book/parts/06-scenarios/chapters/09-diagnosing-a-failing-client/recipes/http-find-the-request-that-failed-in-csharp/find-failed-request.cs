// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A client run of fifty requests against an in-process server; afterwards the test narrows the
// recorded exchanges to each answer of 400 or more and the request that caused it.
// Run: dotnet run find-failed-request.cs [-- clean]
//   (no argument)  fetch reports 1 to 50; report 37 is missing
//   clean          fetch reports 1 to 36, which are all there
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Output;

int last = args.Length > 0 && args[0] == "clean" ? 36 : 50;

// The server records every exchange and keeps its verbose log in memory: reports 1 to 50 but 37.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
foreach (int n in Enumerable.Range(1, 50).Where(n => n != 37))
{
    options.Files[$"reports/report-{n}.csv"] = Encoding.ASCII.GetBytes($"id,total\n{n},40\n");
}
await using var server = await SurlServer.StartAsync(options);

// The client run: one client, one request after another.
int ok = 0;
await using (CurlClient client = CurlClient.Create())
{
    for (int n = 1; n <= last; n++)
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}reports/report-{n}.csv"), CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        if (response.StatusCode == 200) ok++;
    }
}
Console.WriteLine($"run: {last} requests, {ok} answered 200");

// The record is whole once every exchange that started has ended: wait for that, at most 10 s.
var started = new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted };
var ended = new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded };
for (var clock = System.Diagnostics.Stopwatch.StartNew(); server.FindEvents(ended).Count() < server.FindEvents(started).Count();)
{
    if (clock.Elapsed > TimeSpan.FromSeconds(10)) { Console.WriteLine("the exchanges did not end within 10 s"); return 1; }
    await Task.Delay(100);
}
Console.WriteLine($"exchanges: {server.Exchanges.Count}");

// Walk every exchange's chunks in the order they crossed: a received chunk that starts with a
// request line is a request; a sent chunk that starts with a status of 400 or more is a failing answer.
int requests = 0, failed = 0;
string[] logLines = log.ToString().Split('\n');
foreach (SurlExchangeRecord exchange in server.Exchanges)
{
    string[] request = [];
    foreach (SurlExchangeChunk chunk in exchange.Chunks)
    {
        string[] lines = Encoding.ASCII.GetString(chunk.Bytes.Span).Split("\r\n");
        if (chunk.Direction == SurlExchangeDirection.Received && lines[0].EndsWith(" HTTP/1.1", StringComparison.Ordinal))
        {
            requests++;
            request = lines.TakeWhile(l => l.Length > 0).ToArray();
        }
        else if (chunk.Direction == SurlExchangeDirection.Sent && lines[0].StartsWith("HTTP/1.1 ", StringComparison.Ordinal)
            && int.Parse(lines[0].Split(' ')[1]) >= 400)
        {
            failed++;
            Console.WriteLine($"request {requests} of the run, exchange {exchange.ExchangeId}");
            foreach (string line in request) Console.WriteLine($"> {line}");
            // The server's reason: its event line for this request, by method and path.
            string target = string.Join(' ', request[0].Split(' ')[..2]);
            foreach (string line in logLines.Where(l => l.StartsWith($"#{exchange.ExchangeId} * {target}: ", StringComparison.Ordinal)))
            {
                Console.WriteLine($"* {line.TrimEnd('\r')[$"#{exchange.ExchangeId} * ".Length..]}");
            }
            foreach (string line in lines.TakeWhile(l => l.Length > 0).Where(l => !l.StartsWith("Date: ", StringComparison.Ordinal)))
            {
                Console.WriteLine($"< {line}");
            }
            Console.WriteLine();
        }
    }
}
Console.WriteLine($"{requests} requests, {failed} answered 400 or more");
return failed > 0 ? 0 : 1;
