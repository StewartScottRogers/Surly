// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A stand-in for the shop's back end that fails one path: a response override scoped to /rates
// answers 503 for every request under it, and every other path is served from the seeded files.
// Run: dotnet run one-path.cs [-- <case>]
//   (none)    the app's five requests and the status of each (the default)
//   asked     how many times the decision was asked, and about what
//   events    the fault events the server recorded
//   404       the same override answering 404 for a file that is there
//   unread    an answer surl cannot read: the request is served, and the server says why
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Collections.Concurrent;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "status";

// The back end's files: prices, stock, and exchange rates in a folder of their own.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["prices.json"] = """{"widget":4.5}"""u8.ToArray();
options.Files["stock.json"] = """{"widget":12}"""u8.ToArray();
options.Files["rates/eur.json"] = """{"eur":0.92}"""u8.ToArray();
options.Files["rates/usd.json"] = """{"usd":1}"""u8.ToArray();
options.Files["rates-archive.json"] = """{"eur":0.9}"""u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The override: asked only at HTTP requests under /rates, at the command stage, and it always
// answers the same fault. The scope does the choosing; the decision needs no if.
string reply = which switch { "404" => "404", "unread" => "abc", _ => "503" };
var asked = new ConcurrentQueue<string>();
string id = server.Faults.AddOverride(
    new SurlFaultScope { Scheme = "http", Path = "/rates", Stage = FaultStage.Command },
    (point, cancel) =>
    {
        asked.Enqueue($"{point.Command} {point.Path}");
        return ValueTask.FromResult<SurlFault?>(SurlFault.ProtocolError(reply));
    });

// The app's start-up: the five documents it reads, one after another.
string[] paths = ["prices.json", "stock.json", "rates/eur.json", "rates/usd.json", "rates-archive.json"];
foreach (string path in paths)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    if (which is "status" or "404" or "unread")
    {
        Console.WriteLine($"GET /{path}: {response.StatusCode} {response.ReasonPhrase}: {text.TrimEnd()}");
    }
}

switch (which)
{
    case "asked":
        Console.WriteLine($"the decision was asked {asked.Count} times: {string.Join(", ", asked)}");
        break;

    case "events":
        var applied = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Cast<FaultAppliedEvent>().ToList();
        Console.WriteLine($"fault events: {applied.Count}");
        applied.ForEach(e => Console.WriteLine($"{e.Kind} at {e.Stage} on {e.Target}, rule {e.RuleId} is the override: {e.RuleId == id}"));
        break;

    case "unread":
        var failed = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.HookFailed }).Cast<HookFailedEvent>().ToList();
        Console.WriteLine($"hook failures: {failed.Count}; {failed[0].Failure}: {failed[0].Message}");
        break;
}

return 0;
