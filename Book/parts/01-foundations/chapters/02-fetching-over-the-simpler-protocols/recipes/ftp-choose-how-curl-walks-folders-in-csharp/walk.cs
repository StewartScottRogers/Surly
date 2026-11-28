// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches files three folders down from an in-process FTP server with each FtpFileMethod, and reads
// from the recorded exchange which commands each method sent.
// Run: dotnet run walk.cs [-- default|locked|missing|log]
//   (no argument) one file with each method
//   default       a fetch with no method set
//   locked        the same file under a folder whose CWD the server refuses, with each method
//   missing       a missing file, and a missing folder with multicwd and nocwd
//   log           the server's own log of the three fetches
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "methods";

// The server: anonymous FTP, four files, and a rule that answers 550 to every CWD under /locked.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = true,
    RecordExchanges = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
    Faults = { SurlFault.ProtocolError("550 CWD is not allowed here") with { Scheme = "ftp", Command = "CWD", Path = "/locked" } },
};
options.Files["a/b/c/one.txt"] = "one\n"u8.ToArray();
options.Files["d/e/f/two.txt"] = "two\n"u8.ToArray();
options.Files["g/h/i/three.txt"] = "three\n"u8.ToArray();
options.Files["locked/in/there.txt"] = "there\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

switch (which)
{
    case "methods":
        await Fetch("multicwd", FtpFileMethod.MultiCwd, "a/b/c/one.txt");
        await Fetch("singlecwd", FtpFileMethod.SingleCwd, "d/e/f/two.txt");
        await Fetch("nocwd", FtpFileMethod.NoCwd, "g/h/i/three.txt");
        break;
    case "default":
        await Fetch("default", null, "a/b/c/one.txt");
        break;
    case "locked":
        await Fetch("multicwd", FtpFileMethod.MultiCwd, "locked/in/there.txt");
        await Fetch("singlecwd", FtpFileMethod.SingleCwd, "locked/in/there.txt");
        await Fetch("nocwd", FtpFileMethod.NoCwd, "locked/in/there.txt");
        Console.WriteLine(Reply(server.Exchanges[0], "550"));
        break;
    case "missing":
        await Fetch("missing file", FtpFileMethod.MultiCwd, "a/b/c/nope.txt");
        await Fetch("missing folder, multicwd", FtpFileMethod.MultiCwd, "a/nope/x.txt");
        await Fetch("missing folder, nocwd", FtpFileMethod.NoCwd, "a/nope/x.txt");
        break;
    case "log":
        await Fetch("multicwd", FtpFileMethod.MultiCwd, "a/b/c/one.txt");
        await Fetch("singlecwd", FtpFileMethod.SingleCwd, "d/e/f/two.txt");
        await Fetch("nocwd", FtpFileMethod.NoCwd, "g/h/i/three.txt");
        string[] lines = log.ToString().Split('\n');
        Console.WriteLine($"CWD d/e/f logged: {lines.Count(l => l.Contains("< CWD d/e/f"))}");
        Console.WriteLine($"RETR g/h/i/three.txt logged: {lines.Count(l => l.Contains("< RETR g/h/i/three.txt"))}");
        Console.WriteLine($"CWD into g, h or i logged: {lines.Count(l => l.Contains("< CWD g") || l.Contains("< CWD h") || l.Contains("< CWD i"))}");
        break;
}

return 0;

// One fetch on a client of its own, so it has a control connection and an exchange of its own.
async Task Fetch(string label, FtpFileMethod? method, string path)
{
    int before = server.Exchanges.Count;
    string outcome;
    try
    {
        await using CurlClient client = CurlClient.Create();
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}")
            {
                Ftp = method is null ? null : new CurlFtpOptions { FileMethod = method.Value },
            }, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        outcome = text.TrimEnd();
    }
    catch (CurlException failure)
    {
        outcome = $"{(int)failure.Code} {failure.Code}";
    }

    await WaitForEndsAsync();
    Console.WriteLine($"{label}: {Commands(server.Exchanges[before])} -> {outcome}");
}

// The path commands of one exchange, in order: CWD with its argument, SIZE and RETR with theirs.
static string Commands(SurlExchangeRecord exchange) =>
    string.Join(", ", Encoding.ASCII.GetString(exchange.Received).Split("\r\n")
        .Where(line => line.StartsWith("CWD ") || line.StartsWith("SIZE ") || line.StartsWith("RETR ")));

// The first line the server sent that starts with a reply code.
static string Reply(SurlExchangeRecord exchange, string code) =>
    Encoding.ASCII.GetString(exchange.Sent).Split("\r\n").First(line => line.StartsWith(code));

// The server records an exchange a moment after the client has gone, so wait (at most 10 s) until
// every exchange it started has ended before reading the record.
async Task WaitForEndsAsync()
{
    var started = new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted };
    var ended = new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded };
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); server.FindEvents(ended).Count < server.FindEvents(started).Count && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(100);
    }
}
