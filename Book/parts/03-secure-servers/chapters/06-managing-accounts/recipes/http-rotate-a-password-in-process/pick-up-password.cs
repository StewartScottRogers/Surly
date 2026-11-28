// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Changes an account's password on an in-process server mid-test and checks that the app picks
// up the new one from its secret file without a restart.
// Run: dotnet run pick-up-password.cs [-- rereads|caches]
//   rereads  the app reads its secret again after a 401 and tries once more (the default)
//   caches   the app read its secret once at start and keeps it
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "rereads";

// The server: one account, app, with the password the app's secret file holds.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Accounts = { new SurlAccount("app", "old-secret") },
    RecordExchanges = true,
};
options.Files["report.txt"] = "report from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
string secretFile = Path.GetFullPath("app-secret.txt");
File.WriteAllText(secretFile, "old-secret");

// The app runs for the whole test: one client, so one connection, and the password as it read it.
await using CurlClient client = CurlClient.Create();
string password = File.ReadAllText(secretFile);
Console.WriteLine($"before: {await FetchReport()}");

// Mid-test: the password changes on the server and in the app's secret file.
try
{
    server.Accounts.Add("app", "new-secret");
}
catch (ArgumentException)
{
    Console.WriteLine("Accounts.Add for app: ArgumentException, the account exists");
}

Console.WriteLine($"Accounts.Remove app: {server.Accounts.Remove("app")}");
server.Accounts.Add("app", "new-secret");
File.WriteAllText(secretFile, "new-secret");
Console.WriteLine($"after: {await FetchReport()}");

// What the server judged for app since the change, in order.
var logins = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged, Account = "app" }).Cast<LoginJudgedEvent>().Skip(1);
Console.WriteLine($"server, after the change: {string.Join(", ", logins.Select(l => l.IsAccepted ? "accepted" : "refused"))}");
Console.WriteLine($"exchanges: {server.Exchanges.Count}");
return 0;

// The app's one call: a Digest GET with the password it holds; on a 401 the rereading app reads
// its secret file again and tries once more.
async Task<string> FetchReport()
{
    for (int attempt = 1; ; attempt++)
    {
        var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}report.txt")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app", Password = password, HttpAuthentication = HttpAuthenticationMethods.Digest },
        };
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);
        if (response.StatusCode != 401 || mode == "caches" || attempt == 2)
        {
            return $"{response.StatusCode} after {attempt} attempt(s)";
        }

        password = File.ReadAllText(secretFile);
    }
}
