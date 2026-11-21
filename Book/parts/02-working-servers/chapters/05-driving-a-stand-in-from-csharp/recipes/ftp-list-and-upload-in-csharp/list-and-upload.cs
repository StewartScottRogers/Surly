// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Lists a folder of an in-process FTP server, uploads a file into it and lists it again.
// Run: dotnet run list-and-upload.cs [-- subfolder|missing|listings-off|uploads-off|no-login|login]
//   (no argument) list the root, upload notes.txt, list again, read it back from the store
//   subfolder     list a subfolder, named with its trailing slash
//   missing       upload into a folder that is not there, then again with folders created
//   listings-off  a server that does not answer listings
//   uploads-off   a server that accepts no uploads
//   no-login      a server that accepts no anonymous login
//   login         list the root and print the login the server received
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "list";

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = which != "no-login",
    AllowUploads = which != "uploads-off",
    ListDirectories = which != "listings-off",
    RecordExchanges = true,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
options.Files["sub/a.txt"] = "a\n"u8.ToArray();
options.Files[".hidden"] = "not for you\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

try
{
    switch (which)
    {
        case "subfolder":
            await List("sub/");
            break;
        case "login":
            await List("");
            Console.WriteLine($"received: {string.Join(", ", Received().Where(l => l.StartsWith("USER ")))}");
            break;
        case "missing":
            await Upload("new/notes.txt", create: false);
            break;
        default:
            await List("");
            await Upload("notes.txt", create: false);
            await List("");
            break;
    }
}
catch (CurlException failure)
{
    // The same numbers curl exits with: 9, 19, 25 or 67.
    Console.WriteLine($"{which}: CurlException {(int)failure.Code} {failure.Code}");
}

if (which == "list")
{
    SurlFileContents stored = await server.Files.ReadAsync("/notes.txt");
    Console.Write($"store: {Encoding.UTF8.GetString(stored.Bytes.Span)}");
}

if (which == "missing")
{
    await Upload("new/notes.txt", create: true);
    SurlFolderPage page = await server.Files.ListAsync("/new");
    Console.WriteLine($"created: {string.Join(" ", page.Entries.Select(e => e.Name))}");
}

return 0;

// A GET on a folder URL, which ends in a slash, is the long listing, as curl without -l asks for it.
// Each line is printed as its type letter, d for a folder, and its name.
async Task List(string folder)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{folder}"), CancellationToken.None);
    await using Stream body = response.Body;
    string listing = await new StreamReader(body).ReadToEndAsync();
    string[] lines = listing.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
    Console.WriteLine($"/{folder}: {string.Join(", ", lines.Select(l => $"{l[0]} {l[(l.LastIndexOf(' ') + 1)..]}"))}");
}

// curl -T: a PUT with a body stores the file, with --ftp-create-dirs when asked.
async Task Upload(string path, bool create)
{
    var request = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{path}")
    {
        Body = "uploaded by curl\n"u8.ToArray(),
        Ftp = new CurlFtpOptions { CreateMissingDirectories = create ? FtpCreateMissingDirectories.Create : FtpCreateMissingDirectories.None },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    Console.WriteLine($"uploaded /{path}");
}

// What the server received on the control connection, line by line.
IEnumerable<string> Received() => Encoding.ASCII.GetString(server.Exchanges[0].Received).Split("\r\n");
