// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Refuses every login of one account on an in-process server, runs a client with a retry policy
// against it, and counts from the server's recorded events how many times the client tried.
// Run: dotnet run login-once.cs [-- careful|hammering|no-rule]
//   careful    the client retries a 503 but stops at a 401 (the default)
//   hammering  the client retries every failed status, the 401 included
//   no-rule    the careful client against the same server without the rule
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "careful";

// Two accounts; the rule refuses every login of app, whatever its password.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Accounts = { new SurlAccount("app", "s3cret"), new SurlAccount("other", "o-secret") },
    RecordExchanges = true,
};
if (mode != "no-rule")
{
    options.Faults.Add(SurlFault.RefuseLogin() with { Account = "app" });
}

options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The client under test: up to three retries, and a rule for which statuses are worth one.
await using CurlClient client = CurlClient.Create();
Func<int, bool> worthRetrying = mode == "hammering" ? status => status >= 400 : status => status is 429 or 503;
(int status, int retries) = await GetWithRetries("app", "s3cret", maxRetries: 3, worthRetrying);
Console.WriteLine($"app: {status} after {retries} retries");
Console.WriteLine($"other: {(await GetWithRetries("other", "o-secret", 3, worthRetrying)).Status}");

// What the server saw: every login it judged for app, and how many the rule refused.
var logins = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged, Account = "app" }).Cast<LoginJudgedEvent>().ToList();
Console.WriteLine($"logins judged for app: {logins.Count}, refused by the rule: {logins.Count(l => l.IsRefusedByFault)}");
return status == 200 ? 0 : 22;

// One GET with Digest, retried while the policy says so; the last status and the retries made.
async Task<(int Status, int Retries)> GetWithRetries(string user, string password, int maxRetries, Func<int, bool> retry)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
    {
        Authentication = new CurlAuthenticationOptions { UserName = user, Password = password, HttpAuthentication = HttpAuthenticationMethods.Digest },
    };
    for (int tries = 0; ; tries++)
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using (Stream body = response.Body)
        {
            await body.CopyToAsync(Stream.Null);
        }

        if (response.StatusCode < 400 || tries == maxRetries || !retry(response.StatusCode))
        {
            return (response.StatusCode, tries);
        }
    }
}
