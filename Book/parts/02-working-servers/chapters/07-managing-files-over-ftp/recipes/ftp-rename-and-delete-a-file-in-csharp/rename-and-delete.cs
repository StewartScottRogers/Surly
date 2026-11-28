// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Renames and deletes files on an in-process FTP server by sending FTP commands with Curl.Api, and checks
// the server's store afterwards.
// Run: dotnet run rename-and-delete.cs [-- delete|taken|missing|uploads-off|no-login]
//   (no argument) rename draft.txt to final.txt, the replies, the folder listing and the store
//   delete        a delete sent before the listing, and a delete sent after it
//   taken         a rename onto a name that is taken
//   missing       a rename of, and a delete of, a file that is not there, and a rename with no source
//   uploads-off   a server that accepts no uploads
//   no-login      a server that accepts no anonymous login
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "rename";

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = which != "no-login",
    AllowUploads = which != "uploads-off",
    ListDirectories = true,
};
foreach (string name in new[] { "draft.txt", "scratch.txt", "keep.txt", "taken.txt" })
{
    options.Files[name] = Encoding.UTF8.GetBytes(name == "keep.txt" ? "keep me\n" : $"{name}\n");
}

await using SurlServer server = await SurlServer.StartAsync(options);

try
{
    switch (which)
    {
        case "rename":
            // Quote is curl -Q: commands sent before the transfer, which here is a listing of the folder.
            await Send(new CurlFtpOptions { Quote = ["RNFR draft.txt", "RNTO final.txt"] }, replies: true);
            await Store();
            break;
        case "delete":
            await Send(new CurlFtpOptions { Quote = ["DELE scratch.txt"] }, replies: false);
            // PostQuote is curl -Q with a leading dash: sent after the transfer.
            await Send(new CurlFtpOptions { PostQuote = ["DELE draft.txt"] }, replies: false);
            await Store();
            break;
        case "taken":
            await Send(new CurlFtpOptions { Quote = ["RNFR keep.txt", "RNTO taken.txt"] }, replies: false);
            SurlFileContents taken = await server.Files.ReadAsync("/taken.txt");
            Console.WriteLine($"taken.txt: {Encoding.UTF8.GetString(taken.Bytes.Span).Replace("\n", "\\n")}");
            await Store();
            break;
        case "missing":
            foreach (string[] commands in new string[][] { ["RNFR nothere.txt", "RNTO y.txt"], ["DELE nothere.txt"], ["RNTO z.txt"] })
            {
                try
                {
                    await Send(new CurlFtpOptions { Quote = commands }, replies: false);
                }
                catch (CurlException failure)
                {
                    Console.WriteLine($"{commands[0]}: CurlException {(int)failure.Code} {failure.Code}");
                }
            }

            break;
        default:
            await Send(new CurlFtpOptions { Quote = ["RNFR draft.txt", "RNTO final.txt"] }, replies: false);
            break;
    }
}
catch (CurlException failure)
{
    Console.WriteLine($"{which}: CurlException {(int)failure.Code} {failure.Code}");
    await Store();
}

return 0;

// One request for the folder, so there is a transfer for the commands to come before or after, with the
// FTP settings given. Prints the names in the listing the server sent, and optionally the replies.
async Task Send(CurlFtpOptions ftp, bool replies)
{
    // A client of its own, so each request has a control connection of its own.
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, server.BoundUrl.ToString()) { Ftp = ftp }, CancellationToken.None);
    await using Stream body = response.Body;
    string listing = await new StreamReader(body).ReadToEndAsync();
    string[] names = listing.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.TrimEnd('\r').Split(' ')[^1]).ToArray();
    Console.WriteLine($"listing: {string.Join(" ", names)}");
    if (replies)
    {
        // Every reply on the control connection is a header whose key is the reply line.
        string[] answers = response.Headers.Select(h => h.Key).Where(k => k.StartsWith("350 ") || k.StartsWith("250 ")).ToArray();
        Console.WriteLine($"replies: {string.Join(", ", answers)}");
    }
}

// The store as the server holds it.
async Task Store()
{
    SurlFolderPage page = await server.Files.ListAsync("/");
    Console.WriteLine($"store: {string.Join(" ", page.Entries.Select(e => e.Name))}");
}
