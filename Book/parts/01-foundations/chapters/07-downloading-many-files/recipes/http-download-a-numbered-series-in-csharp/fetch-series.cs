// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Downloads report-1.csv to report-N.csv in turn with one shared CurlClient, saves each one
// that is there into reports/, and reports the numbers that were 404.
// Run: dotnet run fetch-series.cs [-- last]   (the series ends at 12 by default)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Surl.Api;

int last = args.Length > 0 ? int.Parse(args[0]) : 12;

// The server: the series with two gaps, 4 and 9. It records exchanges so the program can count connections.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
foreach (int n in Enumerable.Range(1, 12).Except([4, 9]))
{
    options.Files[$"report-{n}.csv"] = Encoding.ASCII.GetBytes($"month,total\n{n},{n * 100}\n");
}

await using var server = await SurlServer.StartAsync(options);

// The job: one client for the whole series, one request at a time.
var missing = new List<int>();
int saved = 0;
Directory.CreateDirectory("reports");
await using (CurlClient client = CurlClient.Create())
{
    for (int n = 1; n <= last; n++)
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}report-{n}.csv"), CancellationToken.None);
        await using Stream body = response.Body;
        if (response.StatusCode == 404)
        {
            missing.Add(n);
            await body.CopyToAsync(Stream.Null);
            continue;
        }

        await using FileStream file = File.Create(Path.Combine("reports", $"report-{n}.csv"));
        await body.CopyToAsync(file);
        saved++;
    }
}

Console.WriteLine($"saved: {saved}");
Console.WriteLine($"missing: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}");
Console.WriteLine($"connections: {server.Exchanges.Count}");
return missing.Count == 0 ? 0 : 1;
