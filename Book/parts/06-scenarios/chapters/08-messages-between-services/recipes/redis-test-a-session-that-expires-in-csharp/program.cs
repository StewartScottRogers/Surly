// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a session that expires, with Surl.Api and Curl.Api in one process: the test seeds a short-lived
// session in the Redis store, asks the app, and asks again until the app says the user is logged out.
// Run: dotnet run program.cs -- <scenario>
//   solution   a session that lives six seconds, the app before and after, and the store afterwards
//   lifetimes  a session with a minute to live, one with no expiry, and one never seeded
//   strict     a server without AllowPlaintextAuthentication
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "solution";

// One server: a Redis store for the sessions and an HTTP listener for the app's pages, one account for both.
// AllowPlaintextAuthentication is --allow-plaintext-auth: both listeners are plain.
var options = new SurlServerOptions
{
    ListenUrls = { "redis://127.0.0.1:0", "http://127.0.0.1:0" },
    AllowPlaintextAuthentication = scenario != "strict",
};
options.Accounts.Add(new SurlAccount("app", "s3cret"));
options.Files["account.txt"] = "your orders and settings\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
string redis = server.BoundUrls[0].ToString(), http = server.BoundUrls[1].ToString();
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(10) });
var login = new CurlAuthenticationOptions { UserName = "app", Password = "s3cret" };

if (scenario == "strict")
{
    try
    {
        Console.WriteLine($"app: {await App("abc")}");
    }
    catch (CurlException refused)
    {
        Console.WriteLine($"app: CurlException {(int)refused.Code} {refused.Code}: {refused.Message}");
    }

    return 0;
}

// A first Curl.Api request sets the engine up, which takes seconds; make it before the clock starts.
await App("warm-up");

if (scenario == "solution")
{
    await server.Keys.SetAsync("session:abc", "alice", TimeSpan.FromSeconds(6));
    Console.WriteLine($"at once: {await App("abc")}");

    // Ask again once a second, at most fifteen times, until the app says logged out.
    string answer = "";
    for (int attempt = 0; attempt < 15 && !answer.StartsWith("logged out", StringComparison.Ordinal); attempt++)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        answer = await App("abc");
    }

    Console.WriteLine($"later: {answer}");
    Console.WriteLine($"store: {await Stored("session:abc")}");
}
else
{
    await server.Keys.SetAsync("session:def", "bob", TimeSpan.FromSeconds(60));
    Console.WriteLine($"with EX 60: {await Stored("session:def")}");
    await server.Keys.SetAsync("session:ghi", "carol", null);
    Console.WriteLine($"with no expiry: {await Stored("session:ghi")}");
    Console.WriteLine($"never seeded: {await App("nobody")}");
}

return 0;

// The app: looks the session up with a GET over Redis; the body is the value and a line feed, and a lone line feed
// for a missing or expired key. With a user, it shows the account page it fetches over HTTP.
async Task<string> App(string session)
{
    string user = (await Fetch($"{redis}GET/session:{session}")).TrimEnd('\n');
    return user.Length == 0 ? "logged out: sign in again" : $"logged in as {user}: {(await Fetch($"{http}account.txt")).TrimEnd('\n')}";
}

async Task<string> Fetch(string url)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body).ReadToEndAsync();
}

// The store, read from the server's side: the key's value and how long it has left, or that it is gone.
async Task<string> Stored(string key)
{
    try
    {
        SurlKeyContents contents = await server.Keys.GetAsync(key);
        TimeSpan? left = contents.Info.ExpiresAt - DateTimeOffset.UtcNow;
        return $"{contents.Text}, {(left is null ? "no expiry" : left.Value.TotalSeconds is > 0 and <= 60 ? "expires within 60 s" : $"expires in {left}")}";
    }
    catch (SurlNotFoundException)
    {
        return "no such key";
    }
}
