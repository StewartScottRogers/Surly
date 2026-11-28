// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Uploads three files at once with Curl.Api, then lists the folder with Files.ListAsync.
// Run: dotnet run program.cs [-- <case>]
//   (none)          three PUTs to /reports/ at once, logged in (the default)
//   one-at-a-time   the same three, each awaited before the next
//   no-login        the same three at once with no login
//   missing-folder  the same three at once into /nowhere/
//   read-only       the same three at once to a server with uploads off
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "at-once";

// The server: one account, uploads on (unless read-only), and a report already in /reports.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    AllowUploads = which != "read-only",
    RecordExchanges = true,
};
options.Files["reports/old.txt"] = "from last week\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The batch: name and bytes, as a tool would have them.
(string Name, string Text)[] batch = [("a.txt", "alpha\n"), ("b.txt", "bravo\n"), ("c.txt", "charlie\n")];
string folder = which == "missing-folder" ? "nowhere/" : "reports/";
var login = new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret", HttpAuthentication = HttpAuthenticationMethods.Digest };

// One client shared by every request; each PUT is its own task.
await using CurlClient client = CurlClient.Create();
async Task<string> Put((string Name, string Text) file)
{
    var request = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{folder}{file.Name}")
    {
        Authentication = which == "no-login" ? null : login,
        Body = Encoding.UTF8.GetBytes(file.Text),
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await response.Body.DisposeAsync();
    return $"PUT /{folder}{file.Name}: {response.StatusCode} {response.ReasonPhrase}";
}

string[] answers;
if (which == "one-at-a-time")
{
    var list = new List<string>();
    foreach (var file in batch)
    {
        list.Add(await Put(file));
    }

    answers = [.. list];
}
else
{
    answers = await Task.WhenAll(batch.Select(Put));
}

foreach (string answer in answers)
{
    Console.WriteLine(answer);
}

Console.WriteLine($"connections: {server.Exchanges.Count}");

// What the server holds: every name in /reports, read from its store, not over the wire.
SurlFolderPage listing = await server.Files.ListAsync("/reports");
Console.WriteLine($"/reports holds: {string.Join(", ", listing.Entries.Select(e => e.Name))}");

// Like curl -f over the batch: 22 if any file was refused, 0 if every one arrived.
return answers.All(a => a.Contains(": 201 ") || a.Contains(": 204 ")) ? 0 : 22;
