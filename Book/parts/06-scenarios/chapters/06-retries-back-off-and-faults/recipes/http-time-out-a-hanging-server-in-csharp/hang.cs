// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file with Surl.Api, holding the first request for it ten seconds, and fetches it with
// Curl.Api under a time limit. Run: dotnet run hang.cs -- [limit] [retries]
//   limit     timeout (the default): a 2 s limit on the whole transfer, CurlClientOptions.Timeout
//             connect: a 2 s limit on connecting only, CurlClientOptions.ConnectTimeout
//             cancel:  a CancellationToken cancelled after 2 s
//             none:    no limit
//   retries   how many retries the client may make after a timeout (0 if not given)
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Diagnostics;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string limit = args.Length > 0 ? args[0] : "timeout";
int allowed = args.Length > 1 ? int.Parse(args[1]) : 0;

// The server: two files in memory, a rule that holds the first request for hello.txt ten seconds once
// it has arrived, and a record of every event, so the program can count the holds afterwards.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["ready.txt"] = "ready\n"u8.ToArray();
options.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(10)) with { Stage = FaultStage.Command, Path = "/hello.txt", Times = 1 });
await using SurlServer server = await SurlServer.StartAsync(options);

// The client: its limit is a client option, as curl -m and --connect-timeout are, or a token per call.
var clientOptions = limit switch
{
    "timeout" => new CurlClientOptions { Timeout = TimeSpan.FromSeconds(2) },
    "connect" => new CurlClientOptions { ConnectTimeout = TimeSpan.FromSeconds(2) },
    _ => new CurlClientOptions(),
};
await using CurlClient client = CurlClient.Create(clientOptions);

// One fetch first, of a file the rule does not hold: the first request in a process starts the curl
// engine, which can take seconds on a busy machine, and the stopwatch below should time only the transfer.
CurlResponse warm = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}ready.txt"), CancellationToken.None);
await using (Stream ready = warm.Body)
{
    await ready.CopyToAsync(Stream.Null);
}

int retries = 0;
bool served = false;
for (int attempt = 1; ; attempt++)
{
    using var cancel = new CancellationTokenSource();
    if (limit == "cancel")
    {
        cancel.CancelAfter(TimeSpan.FromSeconds(2));
    }

    var clock = Stopwatch.StartNew();
    string outcome;
    try
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), cancel.Token);
        await using Stream body = response.Body;
        outcome = $"{response.StatusCode} {response.ReasonPhrase}, {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}";
        served = true;
    }
    catch (CurlException failure)
    {
        // A transfer that ran out of time is a CurlException carrying curl's exit code.
        outcome = $"CurlException {(int)failure.Code} ({failure.Code})";
    }
    catch (OperationCanceledException failure)
    {
        // A cancelled token is .NET's own cancellation, not one of curl's codes.
        outcome = failure.GetType().Name;
    }

    Console.WriteLine($"attempt {attempt}: {outcome}, {(clock.Elapsed.TotalSeconds < 10 ? "within" : "after")} 10 s");
    if (served || retries == allowed)
    {
        break;
    }

    retries++;
}

// What the server counted: the times the rule held a request.
Console.WriteLine($"fault applied: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count}");

// 0 once the file arrived; 28, curl's code for a timeout, otherwise.
return served ? 0 : 28;
