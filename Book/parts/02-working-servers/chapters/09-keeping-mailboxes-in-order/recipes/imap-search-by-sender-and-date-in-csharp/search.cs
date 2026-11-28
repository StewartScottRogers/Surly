// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Searching a mailbox by sender and date through Curl.Api: a query on an IMAP URL is a SEARCH.
// Run: dotnet run search.cs [-- <case>]
//   (no argument)  FROM shop and SENTSINCE 01-Jun-2026, then fetch the matches by UID
//   substring      FROM in two letter cases
//   name           FROM with a display name
//   since          SINCE a date in the past
//   future         SINCE a date in the future
//   sent           SENTSINCE and SENTBEFORE
//   combine        several criteria in one search
//   or             OR
//   nothing        a search that matches nothing
//   bad-date       a date in the wrong format
//   renumber       the same search after an expunge
//   uid            UID SEARCH, then a fetch of its first UID
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "solution";

// IMAP on a free port, example.com, one mailbox.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
string imap = $"{server.BoundUrls[0]}";

// Four messages with a From and a Date header, put in with DeliverAsync, which returns once each is stored.
string[] mails =
[
    "From: shop@example.com\r\nDate: Mon, 02 Mar 2026 10:00:00 +0000\r\nSubject: Order 1\r\n\r\nFirst.\r\n",
    "From: bank@example.org\r\nDate: Wed, 15 Apr 2026 10:00:00 +0000\r\nSubject: Statement\r\n\r\nYour statement.\r\n",
    "From: shop@example.com\r\nDate: Fri, 10 Jul 2026 10:00:00 +0000\r\nSubject: Order 2\r\n\r\nSecond.\r\n",
    "From: Shop Team <shop@example.com>\r\nDate: Tue, 03 Nov 2026 10:00:00 +0000\r\nSubject: Order 3\r\n\r\nThird.\r\n",
];
foreach (string mail in mails)
{
    await server.Mail.DeliverAsync("ann@example.com", Encoding.ASCII.GetBytes("To: ann@example.com\r\n" + mail));
}

await using CurlClient client = CurlClient.Create();
var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };

// One IMAP request through Curl.Api; the body is the answer. A path with ? is a SEARCH, with ;UID= a FETCH.
async Task<string> Imap(string path, string? command = null)
{
    var request = new CurlRequest(HttpMethod.Get, imap + path) { Authentication = login, CustomRequest = command };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// A search is the query of the folder's URL; %20 is a space.
Task<string> Search(string criteria) => Imap("INBOX?" + criteria.Replace(" ", "%20"));

// The headers of one message by UID, on one line.
async Task<string> Header(int uid) =>
    (await Imap($"INBOX;UID={uid};SECTION=HEADER.FIELDS%20(FROM%20SUBJECT)")).Replace("\r\n", " | ");

switch (mode)
{
    case "solution":
        Console.WriteLine($"search: {await Search("FROM shop SENTSINCE 01-Jun-2026")}");
        Console.WriteLine($"UID 3: {await Header(3)}");
        Console.WriteLine($"UID 4: {await Header(4)}");
        break;

    case "substring":
        Console.WriteLine($"shop: {await Search("FROM shop")}");
        Console.WriteLine($"SHOP: {await Search("FROM SHOP")}");
        break;

    case "name":
        Console.WriteLine(await Search("FROM \"Shop Team\""));
        break;

    case "since":
        Console.WriteLine(await Search("SINCE 01-Jan-2020"));
        break;

    case "future":
        Console.WriteLine(await Search("SINCE 01-Jan-2099"));
        break;

    case "sent":
        Console.WriteLine($"SENTSINCE: {await Search("SENTSINCE 01-Jul-2026")}");
        Console.WriteLine($"SENTBEFORE: {await Search("SENTBEFORE 01-Jul-2026")}");
        break;

    case "combine":
        Console.WriteLine(await Search("FROM shop SENTSINCE 01-Jun-2026 SENTBEFORE 01-Oct-2026"));
        break;

    case "or":
        Console.WriteLine(await Search("OR FROM bank FROM Team"));
        break;

    case "nothing":
        Console.WriteLine(await Search("SINCE 01-Jan-2020 FROM nobody"));
        break;

    case "bad-date":
        try
        {
            await Search("SENTSINCE 2026-11-03");
        }
        catch (CurlException failed)
        {
            // Code is curl's exit code; the exception does not carry the server's BAD line.
            Console.WriteLine($"curl error {(int)failed.Code}, {failed.Message}");
        }

        break;

    case "renumber":
        Console.WriteLine($"before: {await Search("FROM shop")}");
        await Imap("INBOX", @"STORE 1 +FLAGS (\Deleted)");
        await Imap("INBOX", "EXPUNGE");
        Console.WriteLine($"after expunging 1: {await Search("FROM shop")}");
        break;

    case "uid":
        await Imap("INBOX", @"STORE 1 +FLAGS (\Deleted)");
        await Imap("INBOX", "EXPUNGE");
        Console.WriteLine($"UID SEARCH: {await Imap("INBOX", "UID SEARCH FROM shop")}");
        Console.WriteLine($"UID 3: {await Imap("INBOX;UID=3;SECTION=HEADER.FIELDS%20(SUBJECT)")}");
        break;
}

return 0;
