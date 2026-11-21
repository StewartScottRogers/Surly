// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A sync job in a test: each GET carries the entity tag saved with the local copy as If-None-Match,
// and the server answers 304 Not Modified while the tag is current. The server runs on a clock the test moves.
// Run: dotnet run sync.cs -- <case>
//   changed      run, run again, rewrite a.txt five seconds later, run again (the default)
//   same-second  the same, but a.txt is rewritten with the clock not moved
//   deleted      run, delete b.txt on the server, run again
//   older        run, run again, rewrite a.txt with the clock moved five seconds back, run again
//   tags         the changed case, printing each reply's status, ETag and Last-Modified beside the saved tag
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "changed";

// The server: two files in memory, and a clock of the test's own, so file times do not depend on when it runs.
var clock = new TestClock(new DateTimeOffset(2024, 1, 2, 15, 4, 5, TimeSpan.Zero));
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, TimeProvider = clock, RecordExchanges = true };
options.Files["a.txt"] = "alpha\n"u8.ToArray();
options.Files["b.txt"] = "beta\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

// The local side: each file's content and the ETag it came with.
// Each copy is also written to disk, under local-<case>/.
var local = new Dictionary<string, (string Text, string ETag)>();
string folder = Directory.CreateDirectory($"local-{which}").FullName;
bool showTags = which is "tags" or "older" or "same-second";

await Sync(1);
await Sync(2);
switch (which)
{
    case "changed" or "tags":
        clock.Advance(TimeSpan.FromSeconds(5));
        await server.Files.PutAsync("/a.txt", "alpha, revised\n"u8.ToArray());
        Console.WriteLine("a.txt rewritten 5 s later");
        break;
    case "same-second":
        await server.Files.PutAsync("/a.txt", "alpha, revised\n"u8.ToArray());
        Console.WriteLine("a.txt rewritten in the same second");
        break;
    case "older":
        clock.Advance(TimeSpan.FromSeconds(-5));
        await server.Files.PutAsync("/a.txt", "alpha, restored\n"u8.ToArray());
        Console.WriteLine("a.txt rewritten 5 s earlier");
        break;
    case "deleted":
        await server.Files.DeleteAsync("/b.txt");
        Console.WriteLine("b.txt deleted");
        break;
}

await Sync(3);
Console.WriteLine($"local a.txt: {local["a.txt"].Text.TrimEnd()}");
Console.WriteLine($"local b.txt: {local["b.txt"].Text.TrimEnd()}");

// What the server sent: every reply head is in the recorded exchanges.
string sent = string.Concat(server.Exchanges.Select(e => Encoding.ASCII.GetString(e.Sent)));
string received = string.Concat(server.Exchanges.Select(e => Encoding.ASCII.GetString(e.Received)));
Console.WriteLine($"server answered: {Count(received, "GET /")} GET, {Count(sent, " 200 OK")} with 200, {Count(sent, " 304 Not Modified")} with 304");
return 0;

// One run of the job: a GET for each file, with If-None-Match when a tag was saved.
async Task Sync(int run)
{
    var report = new List<string>();
    foreach (string name in new[] { "a.txt", "b.txt" })
    {
        bool had = local.TryGetValue(name, out var saved);
        var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{name}")
        {
            Headers = had ? [new("If-None-Match", saved.ETag)] : [],
        };
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        string Header(string key) => response.Headers.FirstOrDefault(h => h.Key == key).Value ?? "none";
        if (showTags)
        {
            Console.WriteLine($"  {name}: {response.StatusCode}, ETag {Header("ETag")}, Last-Modified {Header("Last-Modified")}, saved {(had ? saved.ETag : "none")}");
        }

        switch (response.StatusCode)
        {
            case 304:
                report.Add($"{name} unchanged");
                break;
            case 404:
                report.Add($"{name} gone, local copy kept");
                break;
            default:
                local[name] = (await new StreamReader(body).ReadToEndAsync(), Header("ETag"));
                await File.WriteAllTextAsync(Path.Combine(folder, name), local[name].Text);
                report.Add($"{name} fetched");
                break;
        }
    }

    Console.WriteLine($"run {run}: {string.Join(", ", report)}");
}

static int Count(string text, string what) => (text.Length - text.Replace(what, "").Length) / what.Length;

// A clock that stands still until the test moves it.
sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
