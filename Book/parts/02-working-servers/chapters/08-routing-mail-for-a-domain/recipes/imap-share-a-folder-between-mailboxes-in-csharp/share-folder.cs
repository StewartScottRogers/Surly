// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of shared folders: an in-process Surl server shares team with Ann and Bob, and ops with
// the group oncall, Bob and Carol. The client lists each member's shared folders with Curl.Api,
// Ann appends to team, and Bob reads what she put there.
// Run: dotnet run share-folder.cs [-- <case>]
//   (no argument)  the test: list each member's shared folders, append as Ann, read as Bob
//   namespace      where the server says shared folders live
//   non-member     Carol tries to read team
//   store          the folders each owner has, as the server's store lists them
//   typo           a member's address misspelt in the options
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "test";

// IMAP on a free port for example.com: three mailboxes, a group, and two shared folders.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
foreach (string who in new[] { "ann", "bob", "carol" })
{
    options.Mailboxes.Add($"{who}@example.com:{who}pass");
}

options.Groups.Add("oncall=bob@example.com,carol@example.com");
options.SharedFolders.Add(mode == "typo" ? "team=ann@example.com,bobb@example.com" : "team=ann@example.com,bob@example.com");
options.SharedFolders.Add("ops=@oncall");

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"not started: {refused.ExitCode}: {refused.Lines[0]}");
    return 2;
}

await using var _ = server;
await using CurlClient client = CurlClient.Create();
string imap = $"{server.BoundUrl}";

switch (mode)
{
    case "namespace":
        Console.WriteLine(await Send("ann", "", custom: "NAMESPACE"));
        return 0;
    case "non-member":
        Console.WriteLine($"carol reads team: {await Send("carol", "Shared/team;UID=1")}");
        return 1;
    case "store":
        foreach (string owner in new[] { "ann@example.com", "bob@example.com", "carol@example.com" })
        {
            var names = (await server.Mail.ListMailboxesAsync(owner)).Select(box => box.Name);
            Console.WriteLine($"{owner}: {string.Join(", ", names)}");
        }

        return 0;
}

// The client's view: each member's LIST, keeping the folders under Shared/ that can be selected.
foreach (string who in new[] { "ann", "bob", "carol" })
{
    Console.WriteLine($"{who} shares: {string.Join(", ", await SharedFolders(who))}");
}

// Ann appends the rota to team: a PUT with a body of known length is an IMAP APPEND.
byte[] rota = "From: ann@example.com\r\nTo: team@example.com\r\nSubject: On-call rota for December\r\n\r\nBob takes the first week.\r\n"u8.ToArray();
await Send("ann", "Shared/team", body: rota);

// Bob reads it back.
string subject = await Send("bob", "Shared/team;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)");
Console.WriteLine($"bob reads team: {subject}");
return subject == "Subject: On-call rota for December" ? 0 : 1;

// The shared folders a member's LIST names: the lines for Shared/..., less the \Noselect parent.
async Task<List<string>> SharedFolders(string who)
{
    string list = await Send(who, "", whole: true);
    return list.Split("\r\n")
        .Where(line => line.StartsWith("* LIST ") && !line.Contains(@"\Noselect"))
        .Select(line => line[(line.LastIndexOf(' ') + 1)..])
        .Where(name => name.StartsWith("Shared/"))
        .ToList();
}

// One Curl.Api request as a member: a GET (a LIST for an empty path), an IMAP command of its own,
// or with a body a PUT. Answers the body's first line, or all of it, or the error.
async Task<string> Send(string who, string path, byte[]? body = null, string? custom = null, bool whole = false)
{
    var request = new CurlRequest(body is null ? HttpMethod.Get : HttpMethod.Put, imap + path)
    {
        Authentication = new CurlAuthenticationOptions { UserName = $"{who}@example.com", Password = $"{who}pass" },
        Body = body is null ? null : CurlContent.FromStream(new MemoryStream(body), body.Length),
        CustomRequest = custom,
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream stream = response.Body;
        string text = await new StreamReader(stream, Encoding.ASCII).ReadToEndAsync();
        return whole ? text : text.Split("\r\n")[0];
    }
    catch (CurlException failure)
    {
        return $"curl error {(int)failure.Code}, {failure.Code}: {failure.Message}";
    }
}
