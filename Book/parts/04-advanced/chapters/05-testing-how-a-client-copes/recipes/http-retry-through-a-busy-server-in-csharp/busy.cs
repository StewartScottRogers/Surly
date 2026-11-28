// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a report that is busy (503) for its first two requests, and fetches it with Curl.Api,
// retrying with one second between tries. Run: dotnet run busy.cs -- [retries] [path] [again]
//   retries   how many retries the client may make (2 if not given; 0 for none)
//   path      the file to fetch (report.txt if not given; health.txt is never busy)
//   again     as a third argument: after the loop, fetch the same path once more, with no retries
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Diagnostics;
using Curl.Api;
using Surl.Api;
using Surl.Output;

int allowed = args.Length > 0 ? int.Parse(args[0]) : 2;
string path = args.Length > 1 ? args[1] : "report.txt";
bool again = args.Length > 2 && args[2] == "again";

// The server: two files in memory, a rule that answers the first two requests for /report.txt 503,
// a verbose log it keeps in a StringWriter, and a record of every event.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true, Log = log, LogLevel = LogLevel.Verbose,
};
options.Files["report.txt"] = "report ready\n"u8.ToArray();
options.Files["health.txt"] = "up\n"u8.ToArray();
options.Faults.Add(SurlFault.ProtocolError("503") with { Scheme = "http", Path = "/report.txt", Times = 2 });
await using SurlServer server = await SurlServer.StartAsync(options);

// The first request in a process has one-time set-up costs: make one, to a path that is never busy,
// before anything is timed.
await using CurlClient client = CurlClient.Create();
await Fetch("health.txt");

// A busy answer is a response, not an exception; these are the statuses curl --retry counts as transient.
int[] transient = [408, 429, 500, 502, 503, 504];
var clock = Stopwatch.StartNew();
int retries = 0, status;
for (int attempt = 1; ; attempt++)
{
    (status, string text) = await Fetch(path);
    Console.Write($"attempt {attempt}: {status}");
    if (!transient.Contains(status) || retries == allowed)
    {
        Console.WriteLine(status == 200 ? $", {text.TrimEnd()}" : "");
        break;
    }

    Console.WriteLine(", waiting 1 s");
    await Task.Delay(TimeSpan.FromSeconds(1));
    retries++;
}

// Timing as a range that holds on a loaded machine: two waits of one second, nowhere near twenty.
Console.WriteLine($"retries: {retries}");
Console.WriteLine(retries == 2 ? $"waited between 2 and 20 s: {clock.Elapsed.TotalSeconds is >= 2 and < 20}" : "waited: n/a");

if (again)
{
    (int later, _) = await Fetch(path);
    Console.WriteLine($"again: {later}");
}

// What the server counted: the requests it logged for the path, the times the rule answered, what is left of it.
string wire = log.ToString();
Console.WriteLine($"server logged GET /{path}: {Count(wire, $" < GET /{path} HTTP/1.1")}");
Console.WriteLine($"fault applied: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count}");
SurlFaultRule rule = server.Faults.List().Single();
Console.WriteLine($"{rule.Id} remaining: {rule.Remaining}");

// Like curl -f: 22 for an error status, 0 otherwise.
return status >= 400 ? 22 : 0;

async Task<(int Status, string Text)> Fetch(string name)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{name}"), CancellationToken.None);
    await using Stream body = response.Body;
    return (response.StatusCode, await new StreamReader(body).ReadToEndAsync());
}

static int Count(string text, string part) => text.Split(part).Length - 1;
