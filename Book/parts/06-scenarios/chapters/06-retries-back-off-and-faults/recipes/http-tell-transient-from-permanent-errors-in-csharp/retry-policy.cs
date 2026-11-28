// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves four paths with Surl.Api, each failing in its own way, and fetches each with Curl.Api
// under one retry policy, then counts what the server saw. Run: dotnet run retry-policy.cs -- [policy]
//   transient  retry 408, 429, 500, 502, 503 and 504, twice at most (the default)
//   all        retry every error status, twice at most
//   none       never retry
// A second argument, such as "retries", prints only the lines that start with it.
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string policy = args.Length > 0 ? args[0] : "transient";

// A second argument, a label, prints only that label's lines, so each case checks one claim.
if (args.Length > 1)
{
    Console.SetOut(new LineFilter(Console.Out, args[1]));
}

// The server: /report.txt is busy (503) for one request; /broken.txt is always 500 and /locked.txt
// always 403; /missing.txt is not there, a plain 404. Its verbose log is kept to count requests.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.Files["report.txt"] = "report ready\n"u8.ToArray();
options.Files["broken.txt"] = "never served\n"u8.ToArray();
options.Files["locked.txt"] = "never served\n"u8.ToArray();
options.Faults.Add(SurlFault.ProtocolError("503") with { Scheme = "http", Path = "/report.txt", Times = 1 });
options.Faults.Add(SurlFault.ProtocolError("500") with { Scheme = "http", Path = "/broken.txt" });
options.Faults.Add(SurlFault.ProtocolError("403") with { Scheme = "http", Path = "/locked.txt" });
await using SurlServer server = await SurlServer.StartAsync(options);

// The policy under test: which statuses are worth another try.
int[] transient = [408, 429, 500, 502, 503, 504];
Func<int, bool> worthRetrying = policy switch
{
    "all" => status => status >= 400,
    "none" => _ => false,
    _ => status => transient.Contains(status),
};

string[] paths = ["report.txt", "missing.txt", "locked.txt", "broken.txt"];
int failures = 0;
await using CurlClient client = CurlClient.Create();
foreach (string path in paths)
{
    (int status, int retries) = await GetWithRetries(path, maxRetries: 2);
    Console.WriteLine($"{path}: {status}, retries {retries}");
    failures += status >= 400 ? 1 : 0;
}

// What the server saw: requests per path in its log, and how often each rule answered.
await server.StopAsync();
string text = log.ToString();
Console.WriteLine($"requests: {string.Join(", ", paths.Select(p => $"{p} {text.Split('\n').Count(l => l.Contains($" < GET /{p} HTTP/1.1"))}"))}");
var applied = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Cast<FaultAppliedEvent>();
Console.WriteLine($"rules applied: {string.Join(", ", applied.GroupBy(e => e.RuleId).Select(g => $"{g.Key} {g.Count()}"))}");
return failures == 0 ? 0 : 22;

// One GET, retried one second apart while the policy says so; the last status and the retries made.
async Task<(int Status, int Retries)> GetWithRetries(string path, int maxRetries)
{
    for (int retries = 0; ; retries++)
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
        await using (Stream body = response.Body)
        {
            await body.CopyToAsync(Stream.Null);
        }

        if (response.StatusCode < 400 || retries == maxRetries || !worthRetrying(response.StatusCode))
        {
            return (response.StatusCode, retries);
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}

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
