// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A response override that counts requests and fails exactly the third, so a test sees one failure
// in the middle of a sequence. The server and the client are both in this process.
// Run: dotnet run program.cs [-- <case>]
//   (none)    five fetches with no retry
//   retry     five fetches, each tried up to three times
//   stop      a client that gives up at the first failure
//   record    what the server recorded about the failure
//   path      the override scoped to /report.txt, with fetches of two files mixed
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "plain";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["report.txt"] = "totals\n"u8.ToArray();
options.Files["other.txt"] = "other\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// One untimed fetch first: the first one in a process takes seconds to set up.
await Fetch("other.txt");

// The override: it counts the requests it is asked about and fails the third with 503.
// Interlocked, because the server calls it on its own threads.
int asked = 0;
var scope = new SurlFaultScope { Scheme = "http", Stage = FaultStage.Command, Path = which == "path" ? "/report.txt" : null };
server.Faults.AddOverride(scope, (point, cancel) =>
{
    SurlFault? answer = Interlocked.Increment(ref asked) == 3 ? SurlFault.ProtocolError("503") : null;
    return ValueTask.FromResult(answer!);
});

switch (which)
{
    case "plain":
    case "record":
        for (int n = 1; n <= 5; n++)
        {
            Console.WriteLine($"request {n}: {await Fetch("report.txt")}");
        }
        if (which == "record")
        {
            var applied = (FaultAppliedEvent)await server.WaitForEventAsync(
                new SurlEventQuery { Kind = SurlEventKind.FaultApplied }, TimeSpan.FromSeconds(30));
            Console.WriteLine($"fault events: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count}; {applied.Kind} on {applied.Target}");
        }
        break;

    case "retry":
        for (int n = 1; n <= 5; n++)
        {
            int attempts = 0, status;
            do
            {
                attempts++;
                status = await Fetch("report.txt");
            }
            while (status >= 500 && attempts < 3);
            Console.WriteLine($"fetch {n}: {status} after {attempts} attempt(s)");
        }
        Console.WriteLine($"the override was asked {asked} times");
        break;

    case "stop":
        for (int n = 1; n <= 5; n++)
        {
            int status = await Fetch("report.txt");
            if (status >= 500)
            {
                Console.WriteLine($"fetch {n} failed with {status}; stopping");
                break;
            }
            Console.WriteLine($"fetch {n}: {status}");
        }
        Console.WriteLine($"the override was asked {asked} times");
        break;

    case "path":
        foreach (string file in new[] { "other.txt", "report.txt", "other.txt", "report.txt", "report.txt", "other.txt" })
        {
            Console.WriteLine($"{file}: {await Fetch(file)}");
        }
        Console.WriteLine($"the override was asked {asked} times");
        break;
}
return 0;

async Task<int> Fetch(string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode;
}
