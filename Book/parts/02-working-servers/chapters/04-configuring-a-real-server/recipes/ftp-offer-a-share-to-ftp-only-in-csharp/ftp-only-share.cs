// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Serves two folders as shares on one in-process server, one limited to FTP, and reads each over FTP,
// FTPS and HTTP with Curl.Api.
// Run: dotnet run ftp-only-share.cs -- MODE
//   ftp, http, ftps  the FTP-only share, drop/report.txt, over each listener
//   open-ftp, open-http  the share with no limit, public/readme.txt, over each
//   root             the names the FTP listing of the root holds
//   http-folder      the FTP-only share's folder over HTTP
//   unknown-scheme   a protocols= value that names no scheme
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Options.Network;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "ftp";

// The folders behind the shares.
string root = Path.Combine(Path.GetTempPath(), "ftp-only-share-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "drop"));
Directory.CreateDirectory(Path.Combine(root, "public"));
File.WriteAllText(Path.Combine(root, "drop", "report.txt"), "totals\n");
File.WriteAllText(Path.Combine(root, "public", "readme.txt"), "open to all\n");

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0", "http://127.0.0.1:0", "ftps://127.0.0.1:0" },
    AllowAnonymous = true,
    ListDirectories = true,
    SelfSigned = true,
    Shares = { "drop=" + Path.Combine(root, "drop") + (which == "unknown-scheme" ? ";protocols=nope" : ";protocols=ftp"), "public=" + Path.Combine(root, "public") },
};

try
{
    await using SurlServer server = await SurlServer.StartAsync(options);
    string ftp = server.BoundUrls[0].ToString();
    string http = server.BoundUrls[1].ToString();
    string ftps = server.BoundUrls[2].ToString();

    string result = which switch
    {
        "ftp" => await Fetch(ftp + "drop/report.txt"),
        "http" => await Fetch(http + "drop/report.txt"),
        "ftps" => await Fetch(ftps + "drop/report.txt"),
        "open-ftp" => await Fetch(ftp + "public/readme.txt"),
        "open-http" => await Fetch(http + "public/readme.txt"),
        "http-folder" => await Fetch(http + "drop/"),
        // The long listing of the root: the last word of each line is a name.
        "root" => string.Join(" ", (await Fetch(ftp)).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd().Split(' ').Last()).Order()),
        _ => throw new ArgumentException($"unknown mode {which}"),
    };
    Console.WriteLine($"{which}: {result}");

    return 0;
}
catch (SurlServerStartException refused)
{
    // The first line surl would have written after "surl: ".
    Console.WriteLine($"refused: {refused.Lines[0]}");
    return 0;
}

// One GET with a client of its own: the body for a 200, the status for anything else.
// A self-signed certificate is accepted here, which is for a test on your own machine only.
async Task<string> Fetch(string url)
{
    await using CurlClient client = CurlClient.Create(new CurlClientOptions
    {
        Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } },
    });
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, url), CancellationToken.None);
    await using Stream body = response.Body;
    string text = await new StreamReader(body).ReadToEndAsync();
    return url.StartsWith("http") && !url.StartsWith("https") && (int)response.StatusCode != 200
        ? ((int)response.StatusCode).ToString()
        : text.TrimEnd();
}
