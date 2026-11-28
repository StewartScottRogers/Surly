// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A helper that fetches with Curl.Api and returns the body of a 200, but throws for a 404 or any
// other error status, as HttpClient's GetStringAsync does. Served by an in-process Surl.Api server.
// Run: dotnet run fetch-or-throw.cs [-- <path>|stopped]
//   (no argument)  fetch report.csv, which the server has
//   missing.csv    fetch a path the server does not have: 404
//   busy.csv       fetch a path the server answers 503 Service Unavailable
//   stopped        stop the server first, then fetch report.csv
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Net;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string path = args.Length > 0 ? args[0] : "report.csv";

// The server: one file in memory, and a fault that answers 503 for /busy.csv.
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["report.csv"] = "id,total\n1,40\n"u8.ToArray();
options.Faults.Add(SurlFault.ProtocolError("503") with { Scheme = "http", Path = "/busy.csv" });
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

string url = $"{server.BoundUrl}{(path == "stopped" ? "report.csv" : path)}";
if (path == "stopped")
{
    await server.StopAsync();
}

try
{
    Console.Write(await Fetch.GetStringAsync(client, url));
    return 0;
}
catch (NotFoundException missing)
{
    Console.WriteLine($"NotFoundException, StatusCode {missing.StatusCode}: {missing.Message}");
    return 1;
}
catch (HttpRequestException failed)
{
    Console.WriteLine($"HttpRequestException, StatusCode {failed.StatusCode}: {failed.Message}");
    return 1;
}
catch (CurlException failed)
{
    Console.WriteLine($"CurlException, Code {failed.Code}: {failed.Message}");
    return 1;
}

static class Fetch
{
    // The body of a 200 as text; a NotFoundException for a 404; an HttpRequestException for any
    // other status of 400 or more. A server that cannot be reached still throws Curl.Api's own
    // CurlException, untouched.
    public static async Task<string> GetStringAsync(CurlClient client, string url, CancellationToken cancellationToken = default)
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), cancellationToken);
        await using Stream body = response.Body;
        if (response.StatusCode == 404)
        {
            throw new NotFoundException(url);
        }

        if (response.StatusCode >= 400)
        {
            throw new HttpRequestException($"GET {url} answered {response.StatusCode} {response.ReasonPhrase}", null, (HttpStatusCode)response.StatusCode);
        }

        return await new StreamReader(body).ReadToEndAsync(cancellationToken);
    }
}

// A 404 as its own type, so a caller can catch it apart from other failures; it is still an
// HttpRequestException with StatusCode NotFound for code that catches those.
sealed class NotFoundException(string url)
    : HttpRequestException($"GET {url} answered 404 Not Found", null, HttpStatusCode.NotFound)
{
    public string Url { get; } = url;
}
