// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test captures everything its in-process Surl.Api server logs, start-up warnings included, in
// memory, and prints it only when the test fails.
// Run: dotnet run read-the-log.cs [-- <case>]
//   (no argument)  verbose log; the test fetches summary.csv, which the server does not have
//   pass           the test fetches report.csv, which it has
//   info           the failing test, with the log at LogLevel.Info
//   error          the failing test, with the log at LogLevel.Error
//   strict         the failing test, without AllowAnonymous
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Surl.Api;
using Surl.Output;

string mode = args.Length > 0 ? args[0] : "verbose";
string path = mode == "pass" ? "report.csv" : "summary.csv";

// The log goes to a StringWriter. The server writes from its own threads, so it is synchronized.
var text = new StringWriter();
TextWriter log = TextWriter.Synchronized(text);
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    AllowAnonymous = mode != "strict",
    Log = log,
    LogLevel = mode switch { "info" => LogLevel.Info, "error" => LogLevel.Error, _ => LogLevel.Verbose },
};
options.Accounts.Add(new SurlAccount("alice", "secret"));
options.Files["report.csv"] = "id,total\n1,40\n"u8.ToArray();

// The test: fetch a file with no login and expect 200.
string? failure = null;
await using (SurlServer server = await SurlServer.StartAsync(options))
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
    await response.Body.DisposeAsync();
    if (response.StatusCode != 200)
    {
        failure = $"expected 200 for {path}, got {response.StatusCode}";
    }
}

// The server has stopped, so the log is complete. A passing test prints nothing of it.
if (failure is null)
{
    Console.WriteLine($"PASS fetched {path}");
    return 0;
}

Console.WriteLine($"FAIL {failure}");
Console.WriteLine("server log:");
foreach (string line in text.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
{
    Console.WriteLine($"  {line.TrimEnd('\r')}");
}

return 1;
