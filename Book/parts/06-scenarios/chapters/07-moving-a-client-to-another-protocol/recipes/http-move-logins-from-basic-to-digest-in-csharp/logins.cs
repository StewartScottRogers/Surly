// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of moving HTTP logins from Basic to Digest: an in-process server accepts both during the move,
// each client logs in its own way, and the server's recorded logins name the clients still on Basic.
// Run: dotnet run logins.cs [-- challenge|wrong|any|cutover]
//   (no argument) the old client with Basic, the new one with Digest, and what the server recorded
//   challenge     what a request with no login is offered during the move
//   wrong         a Basic login with the wrong password during the move
//   any           a client that lets curl pick the method from the challenge
//   cutover       a server that takes Digest only, which is surl's default over plain HTTP
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "move";

// The server: one account per client. Basic over plain HTTP is refused unless
// AllowPlaintextAuthentication says otherwise, so the move is the loosened server and "cutover" is not.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Accounts = { new SurlAccount("billing", "b1lling"), new SurlAccount("search", "s3arch") },
    AllowPlaintextAuthentication = which != "cutover",
    RecordExchanges = true,
    Log = log,
    LogLevel = LogLevel.Verbose,
};
options.Files["report.txt"] = "quarterly report\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

switch (which)
{
    case "move":
        await Get("billing, Basic", "billing", "b1lling", HttpAuthenticationMethods.Basic);
        await Get("search, Digest", "search", "s3arch", HttpAuthenticationMethods.Digest);
        Judged();
        Console.WriteLine($"still sending Basic: {string.Join(", ", Accounts("Basic"))}");
        Console.WriteLine($"moved to Digest: {string.Join(", ", Accounts("Digest"))}");
        Console.WriteLine($"log holds a password: {(log.ToString().Contains("b1lling") || log.ToString().Contains("s3arch") ? "yes" : "no")}");
        break;
    case "challenge":
        await Get("no login", null, null, null);
        break;
    case "wrong":
        await Get("billing, Basic, wrong password", "billing", "wrong", HttpAuthenticationMethods.Basic);
        Judged();
        break;
    case "any":
        await Get("billing, any method", "billing", "b1lling", HttpAuthenticationMethods.Any);
        Judged();
        break;
    case "cutover":
        await Get("billing, Basic", "billing", "b1lling", HttpAuthenticationMethods.Basic);
        await Get("no login", null, null, null);
        await Get("search, Digest", "search", "s3arch", HttpAuthenticationMethods.Digest);
        await Get("billing, any method", "billing", "b1lling", HttpAuthenticationMethods.Any);
        Console.WriteLine($"Basic logins judged: {Accounts("Basic").Count()}");
        int basic = server.Exchanges.Count(x => Encoding.ASCII.GetString(x.Received).Contains("Authorization: Basic "));
        Console.WriteLine($"requests that arrived with Basic: {basic}");
        break;
}

return 0;

// One GET through a client of its own, so a connection of its own: the status and what a 401 or 403 offers.
async Task Get(string label, string? user, string? password, HttpAuthenticationMethods? method)
{
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}report.txt")
    {
        Authentication = user is null ? null : new CurlAuthenticationOptions
        {
            UserName = user,
            Password = password,
            HttpAuthentication = method!.Value,
        },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    Console.WriteLine($"{label}: {response.StatusCode}");
    if (response.StatusCode is 401 or 403)
    {
        var offered = response.Headers
            .Where(h => h.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Value.Split(' ')[0]).ToList();
        Console.WriteLine($"{response.StatusCode} offers: {(offered.Count == 0 ? "nothing" : string.Join(", ", offered))}");
    }
}

// Every login the server judged: method, account, verdict.
void Judged()
{
    foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
    {
        Console.WriteLine($"login judged: {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
    }
}

// The accounts whose logins with one method were accepted: the clients that use it.
IEnumerable<string> Accounts(string mechanism) =>
    server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>()
        .Where(l => l.Mechanism == mechanism && l.IsAccepted).Select(l => l.Account).Distinct();
