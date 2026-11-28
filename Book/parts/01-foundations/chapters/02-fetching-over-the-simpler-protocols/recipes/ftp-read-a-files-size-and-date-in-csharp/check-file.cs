// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Asks an in-process FTP server for a file's size and modification time without downloading it, and
// decides from them whether a local copy is out of date.
// Run: dotnet run check-file.cs [-- sync|missing|refused]
//   (no argument) the size and time of report.csv, and the commands the server received
//   sync          a first run that downloads the file and a second that finds the copy current
//   missing       ask about a file that is not there
//   refused       ask a server that accepts no anonymous login
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Globalization;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "ask";

// The served folder: one file, dated 2 January 2024, 15:04:05 Coordinated Universal Time (UTC).
DirectoryInfo served = Directory.CreateTempSubdirectory("ftp-dates-");
string file = Path.Combine(served.FullName, "report.csv");
File.WriteAllText(file, "id,total\n1,10\n");
File.SetLastWriteTimeUtc(file, new DateTime(2024, 1, 2, 15, 4, 5, DateTimeKind.Utc));
string copy = Path.Combine(Path.GetTempPath(), $"{served.Name}.csv");

try
{
    var options = new SurlServerOptions
    {
        ListenUrls = { "ftp://127.0.0.1:0" },
        DataDirectory = served.FullName,
        AllowAnonymous = which != "refused",
        RecordExchanges = true,
    };
    await using SurlServer server = await SurlServer.StartAsync(options);
    // FetchFileTime is curl -R: every request asks for the remote file's time.
    await using CurlClient client = CurlClient.Create(new CurlClientOptions { FetchFileTime = true });

    // The size and time of one remote file: a HEAD request, which sends MDTM and SIZE.
    async Task<(long Size, DateTimeOffset Modified)> Ask(string name)
    {
        var request = new CurlRequest(HttpMethod.Head, $"{server.BoundUrl}{name}");
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        await body.CopyToAsync(Stream.Null);

        // SIZE arrives as Content-Length and MDTM as Last-Modified, as curl -I prints them.
        string Header(string key) => response.Headers.Single(h => h.Key == key).Value;
        long size = long.Parse(Header("Content-Length"), CultureInfo.InvariantCulture);
        DateTimeOffset modified = DateTimeOffset.ParseExact(Header("Last-Modified"), "r", CultureInfo.InvariantCulture);
        return (size, modified);
    }

    try
    {
        switch (which)
        {
            case "ask":
                (long size, DateTimeOffset modified) = await Ask("report.csv");
                Console.WriteLine($"size: {size}");
                Console.WriteLine($"modified: {modified:yyyy-MM-ddTHH:mm:ssZ}");

                // What the server received, as the commands that matter here. The record can trail the
                // client by a moment, so read it again until it holds the SIZE that was answered, for up to 10 s.
                string received = Encoding.ASCII.GetString(server.Exchanges[0].Received);
                for (var waited = System.Diagnostics.Stopwatch.StartNew(); !received.Contains("SIZE ") && waited.Elapsed.TotalSeconds < 10;)
                {
                    await Task.Delay(100);
                    received = Encoding.ASCII.GetString(server.Exchanges[0].Received);
                }

                Console.WriteLine("commands: " + string.Join(" ", received.Split("\r\n")
                    .Select(line => line.Split(' ')[0]).Where(name => name is "MDTM" or "SIZE" or "RETR" or "EPSV" or "PASV")));
                break;
            case "sync":
                for (int run = 1; run <= 2; run++)
                {
                    (long remoteSize, DateTimeOffset remoteTime) = await Ask("report.csv");
                    var local = new FileInfo(copy);
                    bool current = local.Exists && local.Length == remoteSize && local.LastWriteTimeUtc == remoteTime.UtcDateTime;
                    Console.WriteLine($"run {run}: {(current ? "up to date" : "download")}");
                    if (!current)
                    {
                        CurlResponse download = await client.SendAsync(
                            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}report.csv"), CancellationToken.None);
                        await using (Stream body = download.Body)
                        await using (FileStream output = File.Create(copy))
                        {
                            await body.CopyToAsync(output);
                        }

                        // The copy takes the server's time, as curl -R gives it.
                        File.SetLastWriteTimeUtc(copy, remoteTime.UtcDateTime);
                    }
                }

                break;
            case "missing":
                await Ask("missing.csv");
                break;
            case "refused":
                await Ask("report.csv");
                break;
        }
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{which}: CurlException {(int)failure.Code} {failure.Code}");
    }

    return 0;
}
finally
{
    served.Delete(true);
    File.Delete(copy);
}
