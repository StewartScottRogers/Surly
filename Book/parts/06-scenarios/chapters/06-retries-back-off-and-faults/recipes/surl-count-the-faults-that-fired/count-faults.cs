// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Sets four fault rules on one in-process server, HTTP and FTP, runs a client against it, then
// counts from the server's events which rules fired and how often, and fails if one never did.
// Run: dotnet run count-faults.cs -- [run]
//   all      the client reaches every rule (the default)
//   partial  the client never asks for /export.csv, so the drop rule never fires
//   events   every FaultApplied event, one line each, in order
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string run = args.Length > 0 ? args[0] : "all";

// One server on two listeners sharing one account and one set of files, and four rules.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "ftp://127.0.0.1:0" },
    Accounts = { new SurlAccount("app", "s3cret") },
    AllowPlaintextAuthentication = true,
    RecordExchanges = true,
};
options.Files["report.txt"] = "report ready\n"u8.ToArray();
options.Files["slow/status.txt"] = "fine\n"u8.ToArray();
options.Files["export.csv"] = "id,total\n"u8.ToArray();
options.Faults.Add(SurlFault.ProtocolError("503") with { Scheme = "http", Path = "/report.txt", Times = 2 });
options.Faults.Add(SurlFault.Delay(TimeSpan.FromMilliseconds(300)) with { Stage = FaultStage.Command, Path = "/slow" });
options.Faults.Add(SurlFault.RefuseLogin() with { Scheme = "ftp", Times = 1 });
options.Faults.Add(SurlFault.DropConnection() with { Stage = FaultStage.Command, Path = "/export.csv" });
await using SurlServer server = await SurlServer.StartAsync(options);
string http = server.BoundUrls[0].ToString(), ftp = server.BoundUrls[1].ToString();

// The client under test, as a test would run it: each request answered, refused or dropped.
Console.WriteLine($"GET /report.txt: {await Fetch(http + "report.txt")}, {await Fetch(http + "report.txt")}, {await Fetch(http + "report.txt")}");
Console.WriteLine($"GET /slow/status.txt: {await Fetch(http + "slow/status.txt")}");
Console.WriteLine($"FTP report.txt: {await Fetch(ftp + "report.txt")}, {await Fetch(ftp + "report.txt")}");
if (run != "partial")
{
    Console.WriteLine($"GET /export.csv: {await Fetch(http + "export.csv")}");
}

// The server's side: one FaultApplied event each time a rule acted.
await server.StopAsync();
List<FaultAppliedEvent> applied = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied })
    .Cast<FaultAppliedEvent>().ToList();
if (run == "events")
{
    foreach (FaultAppliedEvent e in applied)
    {
        Console.WriteLine($"{e.RuleId} {e.Kind} at {e.Stage} on {e.Target}");
    }

    return 0;
}

// Every rule, with how often it fired and the uses it has left; a rule that never fired fails the test.
var neverFired = new List<string>();
foreach (SurlFaultRule rule in server.Faults.List())
{
    int fired = applied.Count(e => e.RuleId == rule.Id);
    Console.WriteLine($"{rule.Id} {rule.Item}: fired {fired}, remaining {rule.Remaining?.ToString() ?? "no limit"}");
    if (fired == 0)
    {
        neverFired.Add(rule.Id);
    }
}

Console.WriteLine(neverFired.Count == 0 ? "every rule fired" : $"never fired: {string.Join(", ", neverFired)}");
return neverFired.Count == 0 ? 0 : 1;

// One request from a fresh client: the status, or the CurlException's code.
async Task<string> Fetch(string url)
{
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Get, url)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "app", Password = "s3cret" },
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        return url.StartsWith("ftp") ? "ok" : $"{response.StatusCode}";
    }
    catch (CurlException failure)
    {
        return $"{failure.Code}";
    }
}
