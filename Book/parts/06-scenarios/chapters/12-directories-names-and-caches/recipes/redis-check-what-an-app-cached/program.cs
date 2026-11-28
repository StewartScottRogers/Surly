// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app that caches what its origin sends: the app asks twice, and the test reads the server's own
// record, the origin's requests and the Redis keyspace, to prove the second answer came from the cache.
// Run: dotnet run program.cs [-- case]
//   (no argument)  two calls for one product: one origin request, one key
//   two-products   a second product is a miss of its own: two origin requests, two keys
//   key-gone       the key is deleted between calls, so the next call asks the origin again
//   not-found      the origin answers 404: nothing is cached
//   broken         an app that never writes its cache: the test's check fails
//   no-plaintext   the server keeps its default and refuses the app's password over Redis
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "twice";

// The origin and the cache, one server: two products as HTTP files, and Redis for the app's keys. The app's one
// account covers both, and its password crosses these plain listeners only when AllowPlaintextAuthentication says so.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "redis://127.0.0.1:0" },
    RecordExchanges = true,
    AllowPlaintextAuthentication = which != "no-plaintext",
};
options.Accounts.Add(new SurlAccount("app", "s3cret"));
options.Files["products/1.txt"] = "tea"u8.ToArray();
options.Files["products/2.txt"] = "coffee"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

await using CurlClient client = CurlClient.Create();
var app = new ProductApp(client, server.BoundUrls[0].ToString(), server.BoundUrls[1].ToString(), writesCache: which != "broken");

// A first request through Curl.Api can take seconds to start; a harmless Redis read makes it before the test begins.
try
{
    await app.WarmUpAsync();
}
catch (CurlException refused)
{
    Console.WriteLine($"curl error {(int)refused.Code}, WRONGPASS in the message: {refused.Message.Contains("WRONGPASS")}");
    return (int)refused.Code;
}

Console.WriteLine($"call 1: {await app.GetAsync("1")}");
int afterFirst = await Report();

switch (which)
{
    case "two-products":
        Console.WriteLine($"call 2: {await app.GetAsync("2")}");
        await Report();
        return 0;
    case "key-gone":
        await app.DeleteAsync("1");
        Console.WriteLine($"key deleted: {await Keys()}");
        Console.WriteLine($"call 2: {await app.GetAsync("1")}");
        await Report();
        return 0;
    case "not-found":
        Console.WriteLine($"call 2: {await app.GetAsync("9")}");
        await Report();
        return 0;
    default:
        // The test: the origin must not be asked again, and the key must be there.
        Console.WriteLine($"call 2: {await app.GetAsync("1")}");
        int afterSecond = await Report();
        bool fromCache = afterSecond == afterFirst && await Keys() != "none";
        Console.WriteLine($"second call served from the cache: {fromCache}");
        return fromCache ? 0 : 1;
}

// The test's own proof: what the origin was asked, and what the cache holds.
async Task<int> Report()
{
    int origin = await OriginRequests();
    Console.WriteLine($"origin requests: {origin}");
    Console.WriteLine($"cache keys: {await Keys()}");
    return origin;
}

// The origin's requests so far, from the server's own record of its HTTP connections: the bytes it received,
// one "GET /products/" for each request. The client keeps its connection open, so the record is read until it
// holds as many responses as requests, and at most 10 s.
async Task<int> OriginRequests()
{
    var http = new SurlEventQuery { Scheme = "http" };
    int requests = 0;
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed.TotalSeconds < 10; await Task.Delay(100))
    {
        IReadOnlyList<SurlExchangeRecord> exchanges = server.FindExchanges(http);
        requests = exchanges.Sum(e => Count(e.Received, "GET /products/"));
        if (requests == exchanges.Sum(e => Count(e.Sent, "HTTP/1.1 ")))
        {
            break;
        }
    }

    return requests;
}

static int Count(ReadOnlyMemory<byte> bytes, string what) =>
    System.Text.Encoding.ASCII.GetString(bytes.Span).Split(what).Length - 1;

// The keyspace with no client: each key, its value, and whether it will expire.
async Task<string> Keys()
{
    SurlKeyPage page = await server.Keys.ListAsync("cache:*", null, 100, 0);
    if (page.Keys.Count == 0)
    {
        return "none";
    }

    var found = new List<string>();
    foreach (SurlKeyInfo key in page.Keys.OrderBy(k => k.Key))
    {
        SurlKeyContents contents = await server.Keys.GetAsync(key.Key, 0);
        double left = (contents.Info.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalSeconds;
        found.Add($"{key.Key} = {contents.Text} (expires in about a minute: {left is > 30 and <= 60})");
    }

    return string.Join("; ", found);
}

// The app under test: asks Redis first, asks the origin on a miss, and keeps what the origin said for a minute.
sealed class ProductApp(CurlClient client, string origin, string redis, bool writesCache)
{
    private static readonly CurlAuthenticationOptions Login = new() { UserName = "app", Password = "s3cret" };

    public async Task<string> GetAsync(string id)
    {
        string key = $"cache:product:{id}";
        string cached = await Redis($"GET/{key}");
        if (cached != "")
        {
            return cached;
        }

        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{origin}products/{id}.txt") { Authentication = Login }, CancellationToken.None);
        await using Stream body = response.Body;
        string text = await new StreamReader(body).ReadToEndAsync();
        if (response.StatusCode != 200)
        {
            return "not found";
        }

        if (writesCache)
        {
            await Redis($"SET/{key}/{Uri.EscapeDataString(text)}/EX/60");
        }

        return text;
    }

    public Task WarmUpAsync() => Redis("GET/warm-up");

    public Task DeleteAsync(string id) => Redis($"DEL/cache:product:{id}");

    // A Redis command is the URL path; the answer is its text and a line feed, and null is a lone line feed.
    private async Task<string> Redis(string command)
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, redis + command) { Authentication = Login }, CancellationToken.None);
        await using Stream body = response.Body;
        return (await new StreamReader(body).ReadToEndAsync()).TrimEnd('\n');
    }
}
