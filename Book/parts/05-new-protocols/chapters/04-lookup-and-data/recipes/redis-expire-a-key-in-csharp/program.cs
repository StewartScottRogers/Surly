// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Sets a Redis key with an expiry through Surl.Api, reads it with Surl.Api and Curl.Api while it lives and after it has gone.
// Run: dotnet run program.cs [-- case]
//   (no argument)    a key that lives a minute, read; then one second, read after it has gone
//   no-expiry        a key with no expiry
//   bounds           expiries Surl.Api refuses
//   no-plaintext     log in without AllowPlaintextAuthentication
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "expire";

// The server: a Redis store on a free port and one account, whose password may cross the plain listener.
var options = new SurlServerOptions
{
    ListenUrls = { "redis://127.0.0.1:0" },
    AllowPlaintextAuthentication = which != "no-plaintext",
};
options.Accounts.Add(new SurlAccount("alice", "s3cret"));
await using var server = await SurlServer.StartAsync(options);

// The seed: a string key set as a SET would, with no client and no login. The expiry is a TimeSpan.
if (which == "bounds")
{
    foreach (TimeSpan expiry in new[] { TimeSpan.Zero, TimeSpan.FromDays(366) })
    {
        try
        {
            await server.Keys.SetAsync("session", "abc", expiry);
        }
        catch (ArgumentOutOfRangeException)
        {
            Console.WriteLine($"expiry {expiry}: refused");
        }
    }

    await server.Keys.SetAsync("session", "abc", TimeSpan.FromMilliseconds(1));
    Console.WriteLine("expiry 1 millisecond: accepted");
    return 0;
}

// A minute is far longer than the reads take, even on a slow machine.
await server.Keys.SetAsync("session", "abc", which == "no-expiry" ? null : TimeSpan.FromMinutes(1));
if (which == "no-plaintext")
{
    return await Curl("GET");
}

await Read("before");
await Curl("GET");
if (which == "no-expiry")
{
    return 0;
}

// Set it again with an expiry of one second, then wait two: a slow machine only waits longer.
await server.Keys.SetAsync("session", "abc", TimeSpan.FromSeconds(1));
await Task.Delay(2000);
await Read("after");
await Curl("GET");
return 0;

// The store as the test sees it, read without a client: the value and when it expires.
async Task Read(string when)
{
    try
    {
        SurlKeyContents key = await server.Keys.GetAsync("session");
        Console.WriteLine($"{when}: {key.Info.Type} \"{key.Text}\", expires {(key.Info.ExpiresAt is not { } at ? "never" : at > DateTimeOffset.UtcNow ? "in the future" : "in the past")}");
    }
    catch (SurlNotFoundException)
    {
        Console.WriteLine($"{when}: no such key");
    }
}

// The client: Redis over the URL path, GET session, logged in as alice; null is an empty body.
async Task<int> Curl(string command)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{command}/session")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" },
    };
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string answer = await new StreamReader(body).ReadToEndAsync();
        Console.WriteLine($"curl: {answer.Length} characters, \"{answer.TrimEnd('\n')}\"");
        return 0;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
        return (int)failure.Code;
    }
}
