// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves a file with Surl.Api that is slow and always fails, and fetches it with Curl.Api under a
// retry loop with a time budget. Run: dotnet run budget.cs -- [scenario]
//   budget     every try held 1.5 s then 503; up to 10 retries, 1 s apart, 5 s budget (the default)
//   no-budget  the same server; up to 3 retries and no budget
//   hang       every request held 5 s; each try limited to 1 s, up to 10 retries, 3 s budget
// A second argument, such as "retries", prints only the lines that start with it.
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using System.Diagnostics;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string scenario = args.Length > 0 ? args[0] : "budget";

// A second argument, a label, prints only that label's lines, so each case checks one claim.
if (args.Length > 1)
{
    Console.SetOut(new LineFilter(Console.Out, args[1]));
}
int allowed = scenario == "no-budget" ? 3 : 10;
TimeSpan? budget = scenario switch { "budget" => TimeSpan.FromSeconds(5), "hang" => TimeSpan.FromSeconds(3), _ => null };

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["report.txt"] = "report ready\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
string url = $"{server.BoundUrl}report.txt";

// The first Curl.Api request in a process pays a one-time set-up; make it before any rule and any clock.
await using (CurlClient warmUp = CurlClient.Create())
{
    CurlResponse first = await warmUp.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
    await first.Body.DisposeAsync();
}

// Now the faults: hold each connection 1.5 s and answer every request 503, or hold every request 5 s.
if (scenario == "hang")
{
    server.Faults.Add(SurlFault.Delay(TimeSpan.FromSeconds(5)) with { Stage = FaultStage.Command });
}
else
{
    server.Faults.Add(SurlFault.Delay(TimeSpan.FromMilliseconds(1500)));
    server.Faults.Add(SurlFault.ProtocolError("503") with { Scheme = "http" });
}

// The retry loop. Each try gets a new client, so a new connection; "hang" limits each try to 1 s.
// After a failed try: stop if no retries are left or the budget has passed, else wait 1 s.
var clientOptions = scenario == "hang" ? new CurlClientOptions { Timeout = TimeSpan.FromSeconds(1) } : new CurlClientOptions();
var clock = Stopwatch.StartNew();
int retries = 0;
string last;
while (true)
{
    await using (CurlClient client = CurlClient.Create(clientOptions))
    {
        try
        {
            CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
            await response.Body.DisposeAsync();
            last = $"{response.StatusCode}";
        }
        catch (CurlException failure)
        {
            last = $"{failure.Code} ({(int)failure.Code})";
        }
    }

    if (last == "200" || retries == allowed || (budget is { } b && clock.Elapsed >= b))
    {
        break;
    }

    await Task.Delay(TimeSpan.FromSeconds(1));
    retries++;
}

double seconds = clock.Elapsed.TotalSeconds;
Console.WriteLine($"gave up: {last} after {retries} retries");
Console.WriteLine(scenario switch
{
    "budget" => $"loop took 5 to 10 s: {(seconds >= 5 && seconds < 10 ? "yes" : $"no, {seconds:0.0} s")}",
    "hang" => $"loop took 3 to 8 s: {(seconds >= 3 && seconds < 8 ? "yes" : $"no, {seconds:0.0} s")}",
    _ => $"loop took at least 9 s: {(seconds >= 9 ? "yes" : $"no, {seconds:0.0} s")}",
});

// What the server saw: one use of each rule per try, so tries = retries + 1.
var applied = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Cast<FaultAppliedEvent>().ToList();
Console.WriteLine($"server agrees: {(applied.GroupBy(e => e.RuleId).All(g => g.Count() == retries + 1) ? "yes" : "no")}, rules {string.Join(" ", applied.Select(e => e.RuleId).Distinct())}");
return last == "200" ? 0 : 22;

// Shows only the lines that start with one label, and the indented lines under them.
sealed class LineFilter(TextWriter inner, string label) : TextWriter
{
    private readonly StringBuilder line = new();
    private bool keeping;

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        if (value == '\r')
        {
            return;
        }

        if (value != '\n')
        {
            line.Append(value);
            return;
        }

        string text = line.ToString();
        line.Clear();
        keeping = text.StartsWith(label) || (keeping && text.StartsWith(' '));
        if (keeping)
        {
            inner.Write(text + "\n");
            inner.Flush();
        }
    }
}
