// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Runs an app that writes temporary files over FTP against an in-process Surl.Api server and checks, with the
// server's own Files.ListAsync, that it left none behind. The app is the Upload and Cleanup methods below.
// Run: dotnet run program.cs -- <case>
//   clean            the app deletes both temporary files, the check passes
//   kept             what the folder holds after a clean run
//   leaky-app        the app deletes only the first, and still finishes
//   leftover         the same app, then the check: it fails
//   delete-missing   a cleanup of a file the server does not have
//   client-listing   a server that does not allow listing: a client's listing
//   server-listing   the same server: its own listing
//   uploads-off      a server that takes no uploads: the app
//   nothing-written  the same server: the check
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;

string which = args[0];
bool uploadsOff = which is "uploads-off" or "nothing-written";

// The stand-in: no login, a folder work/ holding one file the app must not remove. A client can list a folder
// only when ListDirectories is set; the server's own Files.ListAsync needs no option.
var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = true,
    AllowUploads = !uploadsOff,
    ListDirectories = which is not ("client-listing" or "server-listing"),
};
options.Files["work/keep.txt"] = Encoding.UTF8.GetBytes("kept\n");
await using SurlServer server = await SurlServer.StartAsync(options);
string folder = $"{server.BoundUrl}work/";

try
{
    switch (which)
    {
        case "clean":
        case "kept":
            await Upload();
            await Cleanup(["a.tmp", "b.tmp"]);
            if (which == "kept") Console.WriteLine($"folder holds: {string.Join(" ", await Names())}");
            else return await Check();
            break;
        case "leaky-app":
            await Upload();
            await Cleanup(["a.tmp"]);
            Console.WriteLine("app finished");
            break;
        case "leftover":
            await Upload();
            await Cleanup(["a.tmp"]);
            return await Check();
        case "delete-missing":
            await Cleanup(["gone.tmp"]);
            break;
        case "server-listing":
            Console.WriteLine($"server lists: {string.Join(" ", await Names())}");
            break;
        case "client-listing":
            await using (CurlClient client = CurlClient.Create())
            {
                await client.SendAsync(new CurlRequest(HttpMethod.Get, folder), CancellationToken.None);
            }

            break;
        case "uploads-off":
            await Upload();
            break;
        default:
            return await Check();
    }
}
catch (CurlException failure)
{
    Console.WriteLine($"refused: curl code {(int)failure.Code} {failure.Code}");
    return (int)failure.Code;
}

return 0;

// The app: writes two temporary files with an FTP upload each, as it does while it works.
async Task Upload()
{
    await using CurlClient client = CurlClient.Create();
    foreach (string name in new[] { "a.tmp", "b.tmp" })
    {
        var put = new CurlRequest(HttpMethod.Put, folder + name) { Body = CurlContent.FromBytes("scratch\n"u8.ToArray()) };
        await client.SendAsync(put, CancellationToken.None);
    }
}

// The app: deletes the files it names. PostQuote is curl -Q with a leading dash, commands sent after the transfer,
// which is a listing of the folder, since a command needs a transfer to ride on. The commands run when the listing
// has been read to its end, and a refused one is thrown then, so the body is read.
async Task Cleanup(string[] names)
{
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Get, folder) { Ftp = new CurlFtpOptions { PostQuote = [.. names.Select(n => $"DELE {n}")] } };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
}

// The names in the folder as the server's store holds them, with no client and no login.
async Task<string[]> Names()
{
    SurlFolderPage page = await server.Files.ListAsync("/work");
    return page.Entries.Select(e => e.Name).Order(StringComparer.Ordinal).ToArray();
}

// The check: fails, exit 1, naming every temporary file left.
async Task<int> Check()
{
    string[] left = (await Names()).Where(n => n.EndsWith(".tmp")).ToArray();
    if (left.Length > 0)
    {
        Console.WriteLine($"leftover: {string.Join(" ", left)}");
        return 1;
    }

    Console.WriteLine("clean");
    return 0;
}
