// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Uploads a report into a dated folder path that does not exist yet, on an in-process FTP server, and lists
// each folder from the server's store.
// Run: dotnet run make-folders.cs [-- without|commands|uploads-off|no-login]
//   (no argument) the upload with folder creation on, and the store listed level by level
//   without       the same upload with it left off
//   commands      two uploads into another new folder, and the commands the server received
//   uploads-off   a server that accepts no uploads, with folder creation on
//   no-login      a server that accepts no anonymous login
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "create";

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = which != "no-login",
    AllowUploads = which != "uploads-off",
    RecordExchanges = true,
};
options.Files["2026/old.txt"] = "already here\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

// One client for the whole run, so every request is on one control connection and one exchange.
await using CurlClient client = CurlClient.Create();

try
{
    switch (which)
    {
        case "create":
            await Upload("2026/11/12/report.txt", create: true);
            foreach (string folder in new[] { "/", "/2026", "/2026/11", "/2026/11/12" })
            {
                await List(folder);
            }

            break;
        case "commands":
            await Upload("2026/11/13/report.txt", create: true);
            await Upload("2026/11/13/report.txt", create: true);
            Console.WriteLine($"commands: {string.Join(", ", await Commands(2))}");
            break;
        default:
            await Upload("2026/11/12/report.txt", create: which != "without");
            break;
    }
}
catch (CurlException failure)
{
    Console.WriteLine($"{which}: CurlException {(int)failure.Code} {failure.Code}");
    await List("/");
}

return 0;

// One upload: curl -T, with --ftp-create-dirs when asked, which is CreateMissingDirectories.Create.
async Task Upload(string path, bool create)
{
    var request = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{path}")
    {
        Body = "totals\n"u8.ToArray(),
        Ftp = new CurlFtpOptions { CreateMissingDirectories = create ? FtpCreateMissingDirectories.Create : FtpCreateMissingDirectories.None },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
}

// One folder of the server's store, names in order, a folder's with a slash after it.
async Task List(string folder)
{
    SurlFolderPage page = await server.Files.ListAsync(folder);
    Console.WriteLine($"{folder}: {string.Join(" ", page.Entries.Select(e => e.IsFolder ? e.Name + "/" : e.Name))}");
}

// The commands that make folders or store files, with the names they carried. The record can trail the
// client by a moment, so look again until it holds a STOR per upload, for up to 10 s.
async Task<string[]> Commands(int uploads)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    while (Recorded().Count(c => c.StartsWith("STOR ")) < uploads && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
    }

    return Recorded();
}

string[] Recorded() =>
    Encoding.ASCII.GetString(server.Exchanges[0].Received).Split("\r\n")
        .Where(line => line.StartsWith("MKD ") || line.StartsWith("STOR ")).ToArray();
