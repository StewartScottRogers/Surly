// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file with Surl.Api that answers its first two requests 503, and fetches it with
// Curl.Api, retrying with a doubling wait. Run: dotnet run flaky.cs -- [retries] [path]
//   retries   how many retries the client may make (3 if not given; 0 for none)
//   path      the path to fetch (hello.txt if not given)
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Surl.Api;

int allowed = args.Length > 0 ? int.Parse(args[0]) : 3;
string path = args.Length > 1 ? args[1] : "hello.txt";

// The server: one file in memory, a rule that answers the first two requests for it 503,
// and a record of every event, so the program can count what the rule did afterwards.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Faults.Add(SurlFault.ProtocolError("503") with { Scheme = "http", Path = "/hello.txt", Times = 2 });
await using SurlServer server = await SurlServer.StartAsync(options);

// The statuses curl --retry counts as transient. Any other status is the answer.
int[] transient = [408, 429, 500, 502, 503, 504];

// The client: the first attempt, then up to `allowed` retries, waiting 1 s, 2 s, 4 s... between them.
await using CurlClient client = CurlClient.Create();
int retries = 0, status;
TimeSpan wait = TimeSpan.FromSeconds(1);
for (int attempt = 1; ; attempt++)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    status = response.StatusCode;
    await using (Stream body = response.Body)
    {
        string text = await new StreamReader(body).ReadToEndAsync();
        Console.Write($"attempt {attempt}: {response.StatusCode} {response.ReasonPhrase}");
        if (!transient.Contains(status) || retries == allowed)
        {
            Console.Write(status == 200 ? $", {text}" : "\n");
            break;
        }
    }

    Console.WriteLine($", waiting {wait.TotalSeconds} s");
    await Task.Delay(wait);
    wait *= 2;
    retries++;
}

// What each side counted: the client its retries, the server the times the rule answered.
Console.WriteLine($"retries: {retries}");
Console.WriteLine($"fault applied: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count}");
SurlFaultRule rule = server.Faults.List().Single();
Console.WriteLine($"{rule.Id} remaining: {rule.Remaining}");

// Like curl -f: 22 for an error status, 0 otherwise.
return status >= 400 ? 22 : 0;
