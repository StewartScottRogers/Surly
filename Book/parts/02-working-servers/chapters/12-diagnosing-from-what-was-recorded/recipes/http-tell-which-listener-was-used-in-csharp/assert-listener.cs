// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that asserts a client used the right listener: one in-process server listens on two HTTP
// URLs and an FTP one, the client fetches a report, and the test reads the ListenUrl of its exchange.
// Run: dotnet run assert-listener.cs [-- app | other | host | ftp | no-anonymous]
//   app (default)  the client uses the app listener: the assertion holds
//   other          the client is configured with the other HTTP listener
//   host           the client connects to the other listener with a Host naming the app listener
//   ftp            the client fetches over the FTP listener
//   no-anonymous   the same FTP fetch on a server without AllowAnonymous
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;

string scenario = args.Length > 0 ? args[0] : "app";

// One server, three listeners on free ports, one report. The log is kept at the default level.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "http://127.0.0.1:0", "ftp://127.0.0.1:0" },
    AllowAnonymous = scenario != "no-anonymous",
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Info,
};
options.Files["report.csv"] = "id,total\n1,40\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
Uri app = server.BoundUrls[0], other = server.BoundUrls[1], ftp = server.BoundUrls[2];
Console.WriteLine($"app listener: {app}");

// The client under test.
var request = scenario switch
{
    "other" => new CurlRequest(HttpMethod.Get, $"{other}report.csv"),
    "host" => new CurlRequest(HttpMethod.Get, $"{other}report.csv") { Headers = [new("Host", app.Authority)] },
    "ftp" or "no-anonymous" => new CurlRequest(HttpMethod.Get, $"{ftp}report.csv"),
    _ => new CurlRequest(HttpMethod.Get, $"{app}report.csv"),
};
try
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
}
catch (CurlException refused)
{
    Console.WriteLine($"client: CurlException {refused.Code}");
    return 1;
}

// Wait, at most 10 s, for the client's exchange to end; the record is then whole.
SurlExchangeRecord exchange = await server.WaitForExchangeAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }, TimeSpan.FromSeconds(10));
string head = Encoding.ASCII.GetString(exchange.Received);
string host = head.Split("\r\n").FirstOrDefault(l => l.StartsWith("Host: ", StringComparison.Ordinal))?[6..] ?? "(none)";
Console.WriteLine($"exchange {exchange.ExchangeId}: {exchange.ListenUrl}, scheme {exchange.Scheme}, Host {host}");

// The log's line for it names the scheme, the listener and the client.
foreach (string line in log.ToString().Split('\n').Where(l => l.Contains(" opened: ", StringComparison.Ordinal)))
{
    Console.WriteLine($"log: {line.TrimEnd('\r')}");
}
Console.WriteLine($"exchanges through the app listener: {server.FindExchanges(new SurlEventQuery { ListenUrl = app }).Count()}");

// The assertion: the client's exchange came through the app listener.
if (exchange.ListenUrl == app)
{
    Console.WriteLine("PASS: the fetch came through the app listener");
    return 0;
}
Console.WriteLine($"FAIL: the fetch came through {exchange.ListenUrl}, not the app listener");
return 1;
