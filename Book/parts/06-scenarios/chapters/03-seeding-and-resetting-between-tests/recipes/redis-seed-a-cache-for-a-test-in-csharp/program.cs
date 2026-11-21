// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds an app's settings and a short-lived cached copy into Redis, and its content into HTTP, through Surl.Api,
// then covers the cache-miss path with Curl.Api. Run: dotnet run program.cs [-- case]
//   (no argument)    seed, read, let the cached copy expire, read again, ask the origin
//   ttl              the seeded keys' time to live, which of them can go
//   miss-now         force the miss with DEL and refill the copy, with no waiting
//   origin           the origin's statuses
//   no-plaintext     log in without AllowPlaintextAuthentication
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "expire";

// The server: Redis and HTTP side by side, one account for both, whose password may cross the plain listeners.
var options = new SurlServerOptions
{
    ListenUrls = { "redis://127.0.0.1:0", "http://127.0.0.1:0" },
    AllowPlaintextAuthentication = which != "no-plaintext",
};
options.Accounts.Add(new SurlAccount("alice", "s3cret"));
// The origin's content, seeded in memory as a file would be on disk.
options.Files["products.json"] = "{\"items\":[\"tea\",\"coffee\"]}\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
string redis = server.BoundUrls[0].ToString();
string http = server.BoundUrls[1].ToString();

// The seed: the setting has no expiry. No client and no login is needed.
await server.Keys.SetAsync("settings:currency", "EUR", null);

await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "no-plaintext":
        return await Refused();
    case "origin":
        Console.WriteLine($"products.json with a login: {await Status(http + "products.json", login: true)}");
        Console.WriteLine($"missing.json with a login: {await Status(http + "missing.json", login: true)}");
        Console.WriteLine($"products.json with no login: {await Status(http + "products.json", login: false)}");
        return 0;
    case "ttl":
        await server.Keys.SetAsync("cache:prices", "seeded", TimeSpan.FromMinutes(1));
        foreach (string key in new[] { "settings:currency", "cache:prices", "never-seeded" })
        {
            Console.WriteLine($"{key}: expires {await Expires(key)}, TTL {await Ttl(key)}");
        }

        return 0;
    case "miss-now":
        await server.Keys.SetAsync("cache:orders", "old", TimeSpan.FromMinutes(1));
        Console.WriteLine($"DEL: {await Curl(redis + "DEL/cache:orders")}");
        Console.WriteLine($"GET: {await Curl(redis + "GET/cache:orders")}");
        await server.Keys.SetAsync("cache:orders", "refilled", TimeSpan.FromMinutes(1));
        Console.WriteLine($"GET after the refill: {await Curl(redis + "GET/cache:orders")}");
        return 0;
}

// The app's first read goes through Curl.Api, which can take seconds to start: it is made before any short expiry runs.
Console.WriteLine($"settings:currency: {await Curl(redis + "GET/settings:currency")}");

// The cached copy lives four seconds. It is read in memory and through the client at once, while it lives.
await server.Keys.SetAsync("cache:products", "cached-copy", TimeSpan.FromSeconds(4));
Console.WriteLine($"cache:products: {await Curl(redis + "GET/cache:products")}, expires {await Expires("cache:products")}");

// Wait one second past its life, and read again. A slow machine only waits longer.
await Task.Delay(5000);
Console.WriteLine($"settings:currency: {await Curl(redis + "GET/settings:currency")}");
Console.WriteLine($"cache:products: {await Curl(redis + "GET/cache:products")}, expires {await Expires("cache:products")}");
Console.WriteLine($"origin: {await Curl(http + "products.json")}");
return 0;

// The body of a request, as alice, with its trailing line feed taken off: null is a lone line feed, so empty, shown as null.
async Task<string> Curl(string url)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" },
    }, CancellationToken.None);
    await using Stream body = response.Body;
    string text = (await new StreamReader(body).ReadToEndAsync()).TrimEnd('\n');
    return text == "" ? "null" : text;
}

// The time to live as Redis counts it: seconds left, -1 with no expiry, -2 with no key.
async Task<string> Ttl(string key)
{
    int seconds = int.Parse(await Curl($"{redis}TTL/{key}"));
    return seconds > 0 ? "positive" : seconds.ToString();
}

// The same key as the test sees it without a client: when it expires, or that it is gone.
async Task<string> Expires(string key)
{
    try
    {
        SurlKeyContents contents = await server.Keys.GetAsync(key);
        return contents.Info.ExpiresAt is { } at ? (at > DateTimeOffset.UtcNow ? "in the future" : "in the past") : "never";
    }
    catch (SurlNotFoundException)
    {
        return "no such key";
    }
}

// The status of an HTTP request; a 404 or a 401 is a response, not an exception.
async Task<int> Status(string url, bool login)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url)
    {
        Authentication = login ? new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" } : null,
    }, CancellationToken.None);
    await using Stream body = response.Body;
    return response.StatusCode;
}

// A login the server refuses is a CurlException, whose Code is the exit code curl would give.
async Task<int> Refused()
{
    try
    {
        await Curl(redis + "GET/settings:currency");
        return 0;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
        return (int)failure.Code;
    }
}
