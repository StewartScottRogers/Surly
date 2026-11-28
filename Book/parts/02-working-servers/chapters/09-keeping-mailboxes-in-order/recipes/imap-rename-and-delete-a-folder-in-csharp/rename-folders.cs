// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A client's folder management through Curl.Api: RENAME and DELETE sent as IMAP commands, with the
// folder list checked in the server's own store.
// Run: dotnet run rename-folders.cs [-- <case>]
//   (no argument)  RENAME Old Archive, then LIST
//   keeps          the renamed folder's messages
//   gone           STATUS of the old name
//   delete         DELETE Drafts
//   no-rename      RENAME a folder that does not exist
//   no-delete      DELETE a folder that is already gone
//   onto           RENAME onto a name in use
//   delete-inbox   DELETE INBOX
//   rename-inbox   RENAME INBOX
//   delete-full    DELETE a folder that holds messages
//   path           RENAME to a name with a slash
//   parent         DELETE the implied parent of that path
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "rename";

// IMAP on a free port, example.com, ann with the folders Old, Drafts and Keep.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass;folders=Old,Drafts,Keep");
await using var server = await SurlServer.StartAsync(options);
string imap = $"{server.BoundUrls[0]}";

await using CurlClient client = CurlClient.Create();
var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };

byte[] Order(int n) => Encoding.ASCII.GetBytes($"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {n} shipped\r\n\r\nOn its way.\r\n");

// Three orders delivered into INBOX from the store; the first two are then moved into Old below.
for (int n = 1; n <= 3; n++)
{
    await server.Mail.DeliverAsync("ann@example.com", Order(n));
}

// One IMAP command, sent as written: CustomRequest takes the place of the method. A URL with no
// folder sends it straight after the login; with one, that folder is selected first. The body is the
// server's untagged answer.
async Task<string> Command(string text, string folder = "")
{
    var request = new CurlRequest(HttpMethod.Get, imap + folder) { Authentication = login, CustomRequest = text };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// Curl.Api 0.2.0 has no IMAP APPEND, so the setup files orders 1 and 2 into Old with MOVE.
await Command("MOVE 1:2 Old", "INBOX");

// A plain GET of the root URL is a LIST.
async Task<string> List()
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, imap) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// The server's own view: each folder and how many messages it holds, read with no client.
async Task<string> Folders()
{
    var boxes = await server.Mail.ListMailboxesAsync("ann@example.com");
    return string.Join(", ", boxes.Select(m => $"{m.Name} {m.MessageCount}"));
}

// A refusal is a CurlException; Code is curl's exit code.
async Task Refused(string text)
{
    try
    {
        await Command(text);
        Console.WriteLine($"{text}: accepted");
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"{text}: curl error {(int)failed.Code}, {failed.Message}");
    }
}

switch (mode)
{
    case "rename":
        await Command("RENAME Old Archive");
        Console.WriteLine(await List());
        break;

    case "keeps":
        await Command("RENAME Old Archive");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "gone":
        await Command("RENAME Old Archive");
        await Refused("STATUS Old (MESSAGES)");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "delete":
        await Command("DELETE Drafts");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "no-rename":
        await Refused("RENAME Nowhere Elsewhere");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "no-delete":
        await Command("DELETE Drafts");
        await Refused("DELETE Drafts");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "onto":
        await Refused("RENAME Keep Old");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "delete-inbox":
        await Refused("DELETE INBOX");
        Console.WriteLine($"folders: {await Folders()}");
        break;

    case "rename-inbox":
        await Refused("RENAME INBOX Mail2025");
        Console.WriteLine($"folders: {await Folders()}");
        Console.WriteLine($"STATUS INBOX: {await Command("STATUS INBOX (MESSAGES)")}");
        break;

    case "delete-full":
        Console.WriteLine($"before: {await Folders()}");
        await Refused("DELETE Old");
        Console.WriteLine($"after: {await Folders()}");
        break;

    case "path":
        await Command("RENAME Keep Work/Notes");
        Console.WriteLine(await List());
        break;

    case "parent":
        await Command("RENAME Keep Work/Notes");
        await Refused("DELETE Work");
        Console.WriteLine($"folders: {await Folders()}");
        break;
}

return 0;
