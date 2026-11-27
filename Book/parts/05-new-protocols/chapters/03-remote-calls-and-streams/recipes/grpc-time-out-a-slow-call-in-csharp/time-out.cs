// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Gives gRPC calls a time limit with Curl.Api against a Surl.Api server in one process, and makes one slow on purpose.
// Run: dotnet run time-out.cs -- MODE
//   gives-up        a call held back 3 s, a client limit of 1 s
//   within-limit    a call held back 200 ms under a limit of 10 s
//   how-long        how long the client waited
//   server-deadline a grpc-timeout header of 300m
//   stream-deadline a stream cut by a 1S grpc-timeout
//   header          whether the limit goes to the server as grpc-timeout
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Diagnostics;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "gives-up";

// The server, in-process: port 0 takes a free one. Its verbose log names the metadata of each call.
var log = new StringWriter();
await using SurlServer server = await SurlServer.StartAsync(new SurlServerOptions
{
    ListenUrls = { "grpc://127.0.0.1:0" },
    AllowAnonymous = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
});
string service = $"{server.BoundUrl}surl.test.Echo/";

// Headers a request carries beside what Curl.Api adds, and the ones it must not send: RemovedHeaders is curl's
// -H "name:", and with it the client's limit stays on the client.
KeyValuePair<string, string>[] noHeaders = [];

// The first Curl.Api request in a process pays a one-time set-up of about four seconds; make it before any timing.
await using (CurlClient warm = CurlClient.Create())
    await Call(warm, "Echo", """{"message":"warm-up"}""", noHeaders);


switch (which)
{
    case "gives-up":
        await using (CurlClient client = Limited(1))
            Console.WriteLine(await Attempt(client, "Echo", """{"message":"x","delayMilliseconds":3000}""", noHeaders, dropTimeout: true));
        break;
    case "within-limit":
        await using (CurlClient client = Limited(10))
            Console.WriteLine(await Attempt(client, "Echo", """{"message":"x","delayMilliseconds":200}""", noHeaders));
        break;
    case "how-long":
    {
        await using CurlClient client = Limited(1);
        var clock = Stopwatch.StartNew();
        await Attempt(client, "Echo", """{"message":"x","delayMilliseconds":3000}""", noHeaders, dropTimeout: true);
        Console.WriteLine(clock.Elapsed.TotalSeconds is >= 1 and < 3 ? "waited between 1 and 3 s" : $"waited {clock.Elapsed.TotalSeconds:F1} s");
        break;
    }
    case "server-deadline":
        await using (CurlClient client = Limited(10))
            Console.WriteLine(await Attempt(client, "Echo", """{"message":"x","delayMilliseconds":3000}""", [new("grpc-timeout", "300m")]));
        break;
    case "stream-deadline":
        await using (CurlClient client = Limited(10))
            Console.WriteLine(await Attempt(client, "Expand", """{"message":"s","count":10,"delayMilliseconds":300}""", [new("grpc-timeout", "1S")]));
        break;
    case "header":
    {
        await using CurlClient client = Limited(5);
        await Call(client, "Echo", """{"message":"x"}""", noHeaders);
        Console.WriteLine($"after a call with the limit: {Named()} lines name grpc-timeout");
        await Call(client, "Echo", """{"message":"x"}""", noHeaders, dropTimeout: true);
        Console.WriteLine($"after a call with the header removed: {Named()} lines name grpc-timeout");
        break;
    }
    default:
        Console.Error.WriteLine($"unknown mode {which}");
        return 2;
}

return 0;

// A client whose whole-call limit is the given seconds, as curl's -m.
static CurlClient Limited(int seconds) => CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(seconds) });

// The log lines that list grpc-timeout among a call's metadata.
int Named() => log.ToString().Split('\n').Count(line => line.Contains("metadata: grpc-timeout"));

// One call: a POST of the request as JSON, the reply read to the end.
async Task<string> Call(CurlClient client, string method, string json, KeyValuePair<string, string>[] headers, bool dropTimeout = false)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Post, service + method) { Body = Encoding.UTF8.GetBytes(json), Headers = headers, RemovedHeaders = dropTimeout ? ["grpc-timeout"] : [] },
        CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// The same call, with what a failure looks like: the code and name curl would exit with, and its message.
async Task<string> Attempt(CurlClient client, string method, string json, KeyValuePair<string, string>[] headers, bool dropTimeout = false)
{
    try
    {
        return await Call(client, method, json, headers, dropTimeout);
    }
    catch (CurlException failed)
    {
        return $"{failed.Code} ({(int)failed.Code}): {failed.Message.Split('\n')[0]}";
    }
}
