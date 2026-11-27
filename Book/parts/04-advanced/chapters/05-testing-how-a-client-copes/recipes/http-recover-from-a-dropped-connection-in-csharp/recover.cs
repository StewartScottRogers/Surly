// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file with Surl.Api, dropping the first request for it, and fetches it with Curl.Api,
// retrying once on a failure. Run: dotnet run recover.cs -- [retries]
//   retries   how many retries the client may make (1 if not given; 0 for none)
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

int allowed = args.Length > 0 ? int.Parse(args[0]) : 1;

// The server: one file in memory, a rule that drops the first request for it once it has
// arrived, and a record of every exchange and event, so the program can count them afterwards.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Faults.Add(SurlFault.DropConnection() with { Stage = FaultStage.Command, Path = "/hello.txt", Times = 1 });
await using SurlServer server = await SurlServer.StartAsync(options);

// The client: the first attempt, then up to `allowed` retries, each after a failure.
await using CurlClient client = CurlClient.Create();
int retries = 0;
CurlCode? failed = null;
for (int attempt = 1; ; attempt++)
{
    try
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt"), CancellationToken.None);
        await using Stream body = response.Body;
        Console.Write($"attempt {attempt}: {response.StatusCode} {await new StreamReader(body).ReadToEndAsync()}");
        failed = null;
        break;
    }
    catch (CurlException failure)
    {
        // A dropped connection is a CurlException, not a response.
        Console.WriteLine($"attempt {attempt}: CurlException {(int)failure.Code} ({failure.Code})");
        failed = failure.Code;
        if (retries == allowed)
        {
            break;
        }

        retries++;
    }
}

// What each side counted: the client its retries, the server its exchanges and the rule's uses.
Console.WriteLine($"retries: {retries}");
Console.WriteLine($"exchanges: {server.Exchanges.Count}");
Console.WriteLine($"fault applied: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count}");
SurlFaultRule rule = server.Faults.List().Single();
Console.WriteLine($"{rule.Id} remaining: {rule.Remaining}");

// 0 once the file arrived; curl's own exit code for the last failure otherwise.
return failed is null ? 0 : (int)failed.Value;
