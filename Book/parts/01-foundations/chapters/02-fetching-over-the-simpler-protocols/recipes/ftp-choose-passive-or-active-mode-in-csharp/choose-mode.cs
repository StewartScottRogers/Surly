// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Fetches one file from an in-process FTP server four ways, and reads from the recorded exchange which
// data-connection command each way sent.
// Run: dotnet run choose-mode.cs [-- other|refused|log]
//   (no argument) default, no EPSV, active, active without EPRT
//   other         active mode naming an address that is not the client's
//   refused       the same fetch from a server that accepts no anonymous login
//   log           the server's own log of a passive and an active fetch
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "modes";

var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = which != "refused",
    RecordExchanges = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

switch (which)
{
    case "modes":
        await Fetch("default", null);
        await Fetch("no EPSV", new CurlFtpOptions { UseEpsv = false });
        await Fetch("active", new CurlFtpOptions { ActiveAddress = "-" });
        await Fetch("active, no EPRT", new CurlFtpOptions { ActiveAddress = "-", UseEprt = false });
        break;
    case "other":
        await Fetch("other address", new CurlFtpOptions { ActiveAddress = "192.0.2.1" });
        break;
    case "refused":
        await Fetch("refused", null);
        break;
    case "log":
        await Fetch("passive", new CurlFtpOptions { UseEpsv = false });
        await Fetch("active", new CurlFtpOptions { ActiveAddress = "-" });
        foreach (string line in log.ToString().Split('\n').Where(l => l.Contains("Data connection opened")))
        {
            Console.WriteLine(line[(line.IndexOf('*') + 2)..].Split(" from ")[0].Split(" to ")[0]);
        }

        break;
}

return 0;

// One fetch on a client of its own, so it has a control connection of its own and so an exchange of its own.
async Task Fetch(string label, CurlFtpOptions? ftp)
{
    try
    {
        int before = server.Exchanges.Count;
        string text;
        await using (CurlClient client = CurlClient.Create())
        {
            CurlResponse response = await client.SendAsync(
                new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt") { Ftp = ftp }, CancellationToken.None);
            await using Stream body = response.Body;
            text = await new StreamReader(body).ReadToEndAsync();
        }

        await WaitForEndsAsync();
        string commands = DataCommands(server.Exchanges[before]);
        Console.WriteLine($"{label}: {commands} -> {text.TrimEnd()}");
    }
    catch (CurlException failure)
    {
        // The failed attempt's exchange is the last one recorded.
        await WaitForEndsAsync();
        Console.WriteLine($"{label}: {DataCommands(server.Exchanges[^1])} -> {(int)failure.Code} {failure.Code}");
    }
}

// The commands of one exchange that open the data connection, in order, with EPRT and PORT cut to their name.
static string DataCommands(SurlExchangeRecord exchange) =>
    string.Join(" ", Encoding.ASCII.GetString(exchange.Received).Split("\r\n")
        .Select(line => line.Split(' ')[0])
        .Where(name => name is "EPSV" or "PASV" or "EPRT" or "PORT"));

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
