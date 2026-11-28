// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that asserts a fetch is answered 200 and, when it is not, prints the exchange behind the
// failure: the server's log lines for it, every byte the client sent and every byte it was answered.
// Run: dotnet run print-on-failure.cs [-- <path> [<password>]]
//   (no argument)  fetch report.csv as app with the right password: the assertion holds
//   <path>         fetch this path instead
//   <password>     log in with this password instead
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Output;

string path = args.Length > 0 ? args[0] : "report.csv";
string password = args.Length > 1 ? args[1] : "s3cret";

// The server records every exchange and keeps its verbose log in memory.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Accounts = { new SurlAccount("app", "s3cret") },
    RecordExchanges = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
};
options.Files["report.csv"] = "id,total\n1,40\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The test: one fetch on a client of its own, so on a connection, and an exchange, of its own.
int before = server.Exchanges.Count;
int status;
await using (CurlClient client = CurlClient.Create())
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "app", Password = password, HttpAuthentication = HttpAuthenticationMethods.Digest },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    status = response.StatusCode;
}

if (status == 200)
{
    Console.WriteLine($"PASS: GET /{path} answered 200");
    return 0;
}

// The assertion failed: print the exchange this test made, so the CI log shows the cause. The server
// records the end of an exchange a moment after its client has gone; wait for that, for up to 10 s.
await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }, TimeSpan.FromSeconds(10));
SurlExchangeRecord exchange = server.Exchanges[before];
Console.WriteLine($"FAIL: GET /{path} answered {status}, expected 200");
Console.WriteLine($"exchange {exchange.ExchangeId} on {exchange.Scheme} from {exchange.Client}");
foreach (string line in log.ToString().Split('\n').Where(l => l.StartsWith($"#{exchange.ExchangeId} * ", StringComparison.Ordinal)))
{
    Console.WriteLine($"  {line.TrimEnd('\r')}");
}

// The bytes in the order they crossed, each line marked > for the client's and < for the server's,
// bodies included: surl's refusals carry a one-line text body.
foreach (SurlExchangeChunk chunk in exchange.Chunks)
{
    string mark = chunk.Direction == SurlExchangeDirection.Received ? ">" : "<";
    string[] lines = Encoding.UTF8.GetString(chunk.Bytes.Span).Split('\n');
    foreach (string line in lines[^1].Length == 0 ? lines.SkipLast(1) : lines)
    {
        Console.WriteLine($"{mark} {line.TrimEnd('\r')}");
    }
}

return 1;
