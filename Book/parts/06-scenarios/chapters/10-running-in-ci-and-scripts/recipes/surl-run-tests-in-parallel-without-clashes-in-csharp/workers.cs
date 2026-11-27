// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Parallel test workers, each with its own in-process server on a free port and its own store:
// nothing is shared but the Surl.Api package.
// Run: dotnet run workers.cs [-- <count> [<worker whose tests use a wrong password>] | shared-directory | shared-port]
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;

if (args is ["shared-directory"] or ["shared-port"])
{
    return await Clash(args[0]);
}

int count = args.Length > 0 ? int.Parse(args[0]) : 4;
int broken = args.Length > 1 ? int.Parse(args[1]) : 0;

// All workers at once; each starts, tests and stops its own server.
WorkerResult[] results = await Task.WhenAll(Enumerable.Range(1, count).Select(n => Worker(n, n == broken ? "wrong" : "s3cret")));
foreach (WorkerResult result in results)
{
    Console.WriteLine(result.Line);
}

Console.WriteLine($"ports: {results.Select(result => result.Port).Distinct().Count()} different");
return results.Any(result => !result.Passed) ? 1 : 0;

// One worker: its own server, its own tests, its own stop.
static async Task<WorkerResult> Worker(int n, string password)
{
    var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, AllowUploads = true };
    options.Accounts.Add(new SurlAccount("tester", "s3cret"));
    options.Files["whoami.txt"] = Encoding.UTF8.GetBytes($"worker {n}\n");
    await using SurlServer server = await SurlServer.StartAsync(options);
    try
    {
        await using CurlClient client = CurlClient.Create();
        var login = new CurlAuthenticationOptions { UserName = "tester", Password = password, HttpAuthentication = HttpAuthenticationMethods.Digest };
        string url = server.BoundUrl.ToString();

        // The tests: upload a result under the name every worker uses, then read it and whoami.txt back.
        CurlResponse put = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{url}result.txt")
        {
            Authentication = login,
            Body = CurlContent.FromBytes(Encoding.UTF8.GetBytes($"result of worker {n}\n")),
        }, CancellationToken.None);
        await put.Body.DisposeAsync();
        if (put.StatusCode != 201)
        {
            return new(n, server.BoundUrl.Port, false, $"worker {n} failed: upload answered {put.StatusCode}");
        }

        string result = await Get(client, $"{url}result.txt", login), whoami = await Get(client, $"{url}whoami.txt", login);

        // What this worker's server recorded, read from its own store.
        SurlFileContents stored = await server.Files.ReadAsync("/result.txt");
        return new(n, server.BoundUrl.Port, true,
            $"worker {n}: {whoami}, {result}; stored {Encoding.UTF8.GetString(stored.Bytes.Span).TrimEnd()}");
    }
    finally
    {
        await server.StopAsync();
    }
}

static async Task<string> Get(CurlClient client, string url, CurlAuthenticationOptions login)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).TrimEnd();
}

// What a worker meets when it shares a data directory or a fixed port with another.
static async Task<int> Clash(string what)
{
    Directory.CreateDirectory("shared");
    await using SurlServer first = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, DataDirectory = "shared" });
    var second = what == "shared-directory"
        ? new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, DataDirectory = "shared" }
        : new SurlServerOptions { ListenUrls = { $"http://127.0.0.1:{first.BoundUrl.Port}" } };
    try
    {
        await using SurlServer clashing = await SurlServer.StartAsync(second);
        Console.WriteLine("second server started");
        return 0;
    }
    catch (SurlServerStartException refused)
    {
        Console.WriteLine($"{what}: {refused.ExitCode} ({(int)refused.ExitCode})");
        return (int)refused.ExitCode;
    }
}

record WorkerResult(int Worker, int Port, bool Passed, string Line);
