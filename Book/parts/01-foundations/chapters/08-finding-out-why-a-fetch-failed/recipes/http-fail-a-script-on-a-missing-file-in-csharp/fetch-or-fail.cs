// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Downloads one file and fails with a clear message when the server does not have it.
// Run: dotnet run fetch-or-fail.cs [-- path [--no-check]]   (the path defaults to report.txt)
//   --no-check   save whatever comes back, as curl without -f does
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Surl.Api;

string path = args.Length > 0 ? args[0] : "report.txt";
bool check = !args.Contains("--no-check");
string target = Path.GetFileName(path);

// The server: one file in memory, on a free port.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["report.txt"] = "the report\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

try
{
    await DownloadAsync($"{server.BoundUrl}{path}", target, check);
    Console.WriteLine($"saved {new FileInfo(target).Length} bytes to {target}");
    return 0;
}
catch (DownloadFailedException e)
{
    Console.Error.WriteLine(e.Message);
    Console.WriteLine($"{target} exists: {File.Exists(target)}");
    return 22;
}

async Task DownloadAsync(string url, string file, bool failOnError)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
    await using Stream body = response.Body;

    // A 404 is a response, not an exception: the status has to be checked here, before a file is opened.
    if (failOnError && response.StatusCode >= 400)
    {
        throw new DownloadFailedException($"download failed: GET {url} answered {response.StatusCode} {response.ReasonPhrase}");
    }

    await using FileStream output = File.Create(file);
    await body.CopyToAsync(output);
}

sealed class DownloadFailedException(string message) : Exception(message);
