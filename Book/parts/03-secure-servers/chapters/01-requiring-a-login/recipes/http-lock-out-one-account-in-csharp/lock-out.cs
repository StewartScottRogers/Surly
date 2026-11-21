// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# file-based app: `dotnet run lock-out.cs -- SCENARIO`.
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string scenario = args.Length > 0 ? args[0] : "lift";

// Two accounts, and a rule that refuses bob's login whatever his password. "calm" leaves the rule out.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Accounts.Add(new SurlAccount("alice", "a1-secret"));
options.Accounts.Add(new SurlAccount("bob", "b2-secret"));
if (scenario != "calm")
    options.Faults.Add(SurlFault.RefuseLogin() with { Account = "bob", Times = scenario == "once" ? 1 : null });
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// One request, logged in with Digest as the named account; the answer is the status line.
async Task<string> Fetch(string user, string password)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt")
    {
        Authentication = new CurlAuthenticationOptions
        {
            UserName = user, Password = password, HttpAuthentication = HttpAuthenticationMethods.Digest,
        },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return $"{(int)response.StatusCode} {response.ReasonPhrase}";
}

switch (scenario)
{
    case "lift":
        Console.WriteLine($"alice: {await Fetch("alice", "a1-secret")}");
        Console.WriteLine($"bob: {await Fetch("bob", "b2-secret")}");
        string id = server.Faults.List().Single().Id;
        Console.WriteLine($"lifted {id}: {server.Faults.Remove(id)}");
        Console.WriteLine($"bob: {await Fetch("bob", "b2-secret")}");
        break;
    case "wrong":
        Console.WriteLine($"bob, right password: {await Fetch("bob", "b2-secret")}");
        Console.WriteLine($"bob, wrong password: {await Fetch("bob", "not-his-password")}");
        break;
    case "calm":
        Console.WriteLine($"bob: {await Fetch("bob", "b2-secret")}");
        break;
    case "once":
        Console.WriteLine($"bob: {await Fetch("bob", "b2-secret")}");
        Console.WriteLine($"bob: {await Fetch("bob", "b2-secret")}");
        Console.WriteLine($"uses left: {server.Faults.List().Single().Remaining}");
        break;
    case "rule":
        SurlFaultRule rule = server.Faults.List().Single();
        Console.WriteLine($"{rule.Id}: {rule.Item}, remaining {(rule.Remaining?.ToString() ?? "no limit")}");
        break;
    case "events":
        await Fetch("alice", "a1-secret");
        await Fetch("bob", "b2-secret");
        foreach (var e in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
            Console.WriteLine($"{e.Mechanism} {e.Account}: {(e.IsAccepted ? "accepted" : "refused")}");
        Console.WriteLine($"exchanges with bob: {server.FindExchanges(new SurlEventQuery { Account = "bob" }).Count}");
        break;
}
return 0;
