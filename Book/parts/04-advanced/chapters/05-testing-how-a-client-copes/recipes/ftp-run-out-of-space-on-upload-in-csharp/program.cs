// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a backup tool's upload against a full disk: an FTP server whose /backups folder is out of
// space, uploads into it and beside it, and a look at what the server kept.
// Run: dotnet run program.cs [-- <case>]
//   (none)     upload tonight's dump into /backups (the default)
//   append     append it to last night's dump
//   staging    upload it to /staging, which has space
//   subfolder  upload it to /backups/2026, a folder made on the way
//   old        upload it to /backups-old, beside /backups
//   fills      /staging fills up while the server runs, then has space again
//   closed     upload it to a server that takes no uploads at all
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "backups";
byte[] tonight = "pg_dump of 2026-11-20\n"u8.ToArray();

// The server: anonymous FTP, uploads on, last night's dump in /backups, and a full disk under /backups.
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = true,
    AllowUploads = which != "closed",
    Faults = { SurlFault.OutOfSpace() with { Path = "/backups" } },
};
options.Files["backups/2026-11-19.dump"] = "pg_dump of 2026-11-19\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

switch (which)
{
    case "append":
        await Upload("backups/2026-11-19.dump", append: true);
        break;
    case "staging":
        await Upload("staging/2026-11-20.dump");
        break;
    case "subfolder":
        await Upload("backups/2026/2026-11-20.dump");
        break;
    case "old":
        await Upload("backups-old/2026-11-20.dump");
        break;
    case "fills":
        // The disk under /staging fills while the server runs, and is freed again.
        await Upload("staging/a.dump");
        string full = server.Faults.Add(SurlFault.OutOfSpace() with { Path = "/staging" });
        await Upload("staging/b.dump");
        server.Faults.Remove(full);
        await Upload("staging/b.dump");
        break;
    default:
        await Upload("backups/2026-11-20.dump");
        break;
}

// The test's check: what the server holds, read from its store without a request.
foreach (string folder in new[] { "/backups", "/backups/2026", "/backups-old", "/staging" })
{
    try
    {
        SurlFolderPage page = await server.Files.ListAsync(folder);
        Console.WriteLine($"{folder}: {string.Join(", ", page.Entries.Select(e => e.IsFolder ? e.Name + "/" : $"{e.Name} {e.ByteCount} bytes"))}");
    }
    catch (SurlNotFoundException)
    {
        Console.WriteLine($"{folder}: none");
    }
}

return 0;

// The backup tool's upload: a PUT with a body on an ftp:// URL is STOR (with Append, APPE).
async Task Upload(string target, bool append = false)
{
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{target}")
        {
            Body = tonight,
            Ftp = new CurlFtpOptions { Append = append, CreateMissingDirectories = FtpCreateMissingDirectories.Create },
        }, CancellationToken.None);
        await response.Body.DisposeAsync();
        Console.WriteLine($"upload {target}: stored");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"upload {target}: {failure.Code} ({failure.Message})");
    }
}
