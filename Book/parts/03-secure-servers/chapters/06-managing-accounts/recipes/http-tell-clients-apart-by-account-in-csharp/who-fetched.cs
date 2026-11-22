// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Two services share one in-process server, each with its own account; the test reads back,
// from the server's recorded events and exchanges and from its log, which account fetched what.
// Run: dotnet run who-fetched.cs [-- events|one-client|not-recorded]
//   events        each service fetches its file, one guesses wrong; read who fetched what (the default)
//   one-client    the same fetches, both made through one client and so one connection
//   not-recorded  the same server with RecordExchanges left off
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "events";

// One server, two accounts; the log is kept in memory at the verbose level.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Accounts = { new SurlAccount("billing", "b1-secret"), new SurlAccount("search", "s1-secret") },
    RecordExchanges = mode != "not-recorded",
    Log = log,
    LogLevel = LogLevel.Verbose,
};
options.Files["invoices.csv"] = "id,total\n1,40\n"u8.ToArray();
options.Files["index.html"] = "<p>search index</p>\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// Each service has its own client, so its own connection, and so has a caller with a wrong
// password; one-client sends all three through one client.
await using (CurlClient billing = CurlClient.Create())
await using (CurlClient search = CurlClient.Create())
await using (CurlClient stranger = CurlClient.Create())
{
    bool one = mode == "one-client";
    Console.WriteLine($"billing GET /invoices.csv: {await Get(billing, "invoices.csv", "billing", "b1-secret")}");
    Console.WriteLine($"search GET /index.html: {await Get(one ? billing : search, "index.html", "search", "s1-secret")}");
    Console.WriteLine($"search, wrong password, GET /invoices.csv: {await Get(one ? billing : stranger, "invoices.csv", "search", "guess")}");
}

// The server records an exchange's end a moment after the client has gone: wait, for at most 10 s,
// until every exchange that started has ended, before reading the record.
for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); options.RecordExchanges && DateTime.UtcNow < deadline; await Task.Delay(100))
{
    if (server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted }).Count == server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).Count) break;
}

IReadOnlyList<SurlEvent> logins;
try
{
    logins = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged });
}
catch (InvalidOperationException off)
{
    Console.WriteLine($"FindEvents: {off.GetType().Name}");
    return 1;
}

// Every login the server judged: the exchange it was in, the method and the account.
foreach (LoginJudgedEvent login in logins.Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"exchange {login.ExchangeId}: {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

// What each account fetched: the request lines of the exchanges in which its login was accepted.
foreach (string account in new[] { "billing", "search" })
{
    var accepted = logins.Cast<LoginJudgedEvent>().Where(l => l.Account == account && l.IsAccepted).Select(l => l.ExchangeId).ToHashSet();
    var paths = server.FindExchanges(new SurlEventQuery { Account = account })
        .Where(x => accepted.Contains(x.ExchangeId))
        .SelectMany(x => Encoding.ASCII.GetString(x.Received).Split("\r\n"))
        .Where(line => line.StartsWith("GET "))
        .Select(line => line.Split(' ')[1])
        .Distinct();
    Console.WriteLine($"{account} fetched: {string.Join(", ", paths)}");
}

// The log says the same in words, and never holds a password.
string text = log.ToString();
Console.WriteLine($"log: {text.Split('\n').Count(l => l.Contains("Login accepted: Digest billing"))} line(s) Login accepted: Digest billing");
Console.WriteLine($"log holds a password: {(text.Contains("b1-secret") || text.Contains("s1-secret") || text.Contains("guess") ? "yes" : "no")}");
return 0;

// One GET as a service makes it, logged in with Digest: the status code.
async Task<int> Get(CurlClient client, string path, string user, string password)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}")
    {
        Authentication = new CurlAuthenticationOptions
        {
            UserName = user,
            Password = password,
            HttpAuthentication = HttpAuthenticationMethods.Digest,
        },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode;
}
