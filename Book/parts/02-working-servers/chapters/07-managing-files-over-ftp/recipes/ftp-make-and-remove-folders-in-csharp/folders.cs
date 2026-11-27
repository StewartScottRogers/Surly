// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Makes and removes folders on an in-process FTP server with MKD and RMD sent as quote commands, and prints the
// server's answer to each and the folders of its store afterwards.
// Run: dotnet run folders.cs -- SCENARIO
//   make            MKD reports
//   inside          MKD reports/2026, with reports there
//   exists          MKD reports, with reports there
//   missing-parent  MKD nowhere/new
//   remove          RMD archive, with archive there and empty
//   not-empty       RMD reports, with a file in it
//   delete-first    RMD, DELE and RMD, in that order, to empty reports and remove it
//   missing         RMD of a name that is a file
//   uploads-off     MKD on a server that accepts no uploads
//   uploads-off-rmd RMD on that server, of a folder with a file in it
//   no-login        MKD on a server that accepts no anonymous login
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Protocols;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "make";
bool seedFile = scenario is "not-empty" or "delete-first" or "uploads-off-rmd";

var options = new SurlServerOptions
{
    ListenUrls = { "ftp://127.0.0.1:0" },
    AllowAnonymous = scenario != "no-login",
    AllowUploads = !scenario.StartsWith("uploads-off"),
    ListDirectories = true,
    RecordExchanges = true,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
if (seedFile)
{
    options.Files["reports/note.txt"] = "a note\n"u8.ToArray();
}

await using SurlServer server = await SurlServer.StartAsync(options);

// What the folders that are there at the start, made with MKD as any client would, before the one that is tried.
string[] before = scenario switch
{
    "inside" or "exists" => ["MKD reports"],
    "remove" => ["MKD archive"],
    "delete-first" => ["MKD reports/2026"],
    _ => [],
};
if (before.Length > 0)
{
    await Send(before);
}

string[] quotes = scenario switch
{
    "make" or "uploads-off" or "no-login" or "exists" => ["MKD reports"],
    "inside" => ["MKD reports/2026"],
    "missing-parent" => ["MKD nowhere/new"],
    "remove" => ["RMD archive"],
    "not-empty" or "uploads-off-rmd" => ["RMD reports"],
    "delete-first" => ["RMD reports/2026", "DELE reports/note.txt", "RMD reports"],
    "missing" => ["RMD hello.txt"],
    _ => throw new ArgumentException($"unknown scenario {scenario}"),
};

int exchangesBefore = server.Exchanges.Count;
string result;
try
{
    await Send(quotes);
    result = "ok";
}
catch (CurlException failure)
{
    result = $"CurlException {(int)failure.Code} {failure.Code}";
}

// The answers to the folder commands, from what the server sent on the connection of the last request.
string replies = await Replies(exchangesBefore);
Console.WriteLine($"{scenario}: {result}; {replies}; store: {await Tree("/")}");
return 0;

// A request for the root listing with the commands as quotes: curl -Q, which runs them before the transfer.
// A quote that the server refuses throws at once, a CurlException with code QuoteError, 21, as curl exits 21.
async Task Send(string[] commands)
{
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Get, server.BoundUrl.ToString()) { Ftp = new CurlFtpOptions { Quote = commands } };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
}

// The server's replies that answer MKD, RMD or the login: a 257 created, 250 Directory, 530 or 550 line. The record can
// trail the client by a moment, so look again until the request's exchange holds one, for up to 10 s.
async Task<string> Replies(int exchange)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    string found = Read(exchange);
    while (found.Length == 0 && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
        found = Read(exchange);
    }

    return found;
}

string Read(int exchange) =>
    server.Exchanges.Count <= exchange ? "" : string.Join(" | ",
        Encoding.ASCII.GetString(server.Exchanges[exchange].Sent).Split("\r\n")
            .Where(line => Regex.IsMatch(line, "^(257 \".+\" created|250 Directory|530|550)")));

// Every folder and file of the store below a path, folders with a slash after them.
async Task<string> Tree(string folder)
{
    SurlFolderPage page = await server.Files.ListAsync(folder);
    var names = new List<string>();
    foreach (SurlFolderEntry entry in page.Entries.OrderBy(e => e.Name, StringComparer.Ordinal))
    {
        string path = folder.TrimEnd('/') + "/" + entry.Name;
        names.Add(path[1..] + (entry.IsFolder ? "/" : ""));
        if (entry.IsFolder)
        {
            string inner = await Tree(path);
            if (inner.Length > 0) names.Add(inner);
        }
    }

    return string.Join(" ", names);
}
