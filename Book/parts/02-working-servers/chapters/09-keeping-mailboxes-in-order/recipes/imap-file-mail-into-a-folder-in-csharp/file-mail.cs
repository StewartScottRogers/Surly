// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A mail rule's filing steps, sent as IMAP commands through Curl.Api and checked in the server's store:
// CREATE a folder, COPY or MOVE a message into it, mark the original deleted and EXPUNGE it.
// Run: dotnet run file-mail.cs [-- <case>]
//   (no argument)  create, copy, mark deleted, expunge
//   exists         CREATE a folder that exists
//   no-folder      COPY into a folder that does not exist
//   no-message     COPY a message number the folder does not have
//   keeps          COPY alone
//   flags          COPY a flagged message
//   deleted        mark a message deleted, no EXPUNGE
//   expunge        EXPUNGE it
//   move           MOVE
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "file";

// IMAP on a free port, example.com, one mailbox with three orders in INBOX.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
string imap = $"{server.BoundUrls[0]}";

// DeliverAsync returns once each order is stored, so no command below waits for mail.
for (int n = 1; n <= 3; n++)
{
    await server.Mail.DeliverAsync("ann@example.com",
        Encoding.ASCII.GetBytes($"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {n} shipped\r\n\r\nOn its way.\r\n"));
}

await using CurlClient client = CurlClient.Create();
var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };

// One IMAP command: CustomRequest is sent as written, spaces and all, in place of the method.
// The folder in the URL is selected first; the body is the server's untagged answer.
async Task<string> Command(string folder, string text)
{
    var request = new CurlRequest(HttpMethod.Get, imap + folder) { Authentication = login, CustomRequest = text };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// A plain GET with a query is a SEARCH.
async Task<string> Search(string folder, string query)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, imap + folder + "?" + query) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// The server's own view: how many messages each folder holds, read with no client.
async Task<string> Counts()
{
    var boxes = await server.Mail.ListMailboxesAsync("ann@example.com");
    return string.Join(", ", boxes.Select(m => $"{m.Name} {m.MessageCount}"));
}

// A refusal is a CurlException; Code is curl's exit code.
async Task Refused(Task<string> command)
{
    try
    {
        await command;
        Console.WriteLine("accepted");
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"curl error {(int)failed.Code}, {failed.Message}");
    }
}

switch (mode)
{
    case "exists":
        await Command("", "CREATE Archive");
        await Refused(Command("", "CREATE Archive"));
        Console.WriteLine($"folders: {await Counts()}");
        return 0;

    case "no-folder":
        await Refused(Command("INBOX", "COPY 1 Nowhere"));
        Console.WriteLine($"folders: {await Counts()}");
        return 0;

    case "no-message":
        await Command("", "CREATE Archive");
        await Refused(Command("INBOX", "COPY 9 Archive"));
        Console.WriteLine($"folders: {await Counts()}");
        return 0;
}

await Command("", "CREATE Archive");

switch (mode)
{
    case "keeps":
        await Command("INBOX", "COPY 2 Archive");
        Console.WriteLine($"after COPY 2: {await Counts()}");
        return 0;

    case "flags":
        await Command("INBOX", @"STORE 2 +FLAGS (\Flagged)");
        await Command("INBOX", "COPY 2 Archive");
        Console.WriteLine($"FLAGGED in Archive: {await Search("Archive", "FLAGGED")}");
        Console.WriteLine($"FLAGGED in INBOX: {await Search("INBOX", "FLAGGED")}");
        return 0;

    case "move":
        Console.WriteLine($"before: {await Counts()}");
        await Command("INBOX", "MOVE 1 Archive");
        Console.WriteLine($"after MOVE 1: {await Counts()}");
        Console.WriteLine($"Archive UIDs: {string.Join(" ", (await server.Mail.ListMessagesAsync("ann@example.com", "Archive")).Messages.Select(m => m.Uid))}");
        return 0;
}

// The filing itself: copy order 1, mark the original deleted, expunge it.
await Command("INBOX", "COPY 1 Archive");
Console.WriteLine($"copied: {await Counts()}");
Console.WriteLine($"STORE: {await Command("INBOX", @"STORE 1 +FLAGS (\Deleted)")}");

if (mode == "deleted")
{
    var inbox = (await server.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages;
    Console.WriteLine($"DELETED in INBOX: {await Search("INBOX", "DELETED")}");
    Console.WriteLine($"store flags of UID 1: {string.Join(" ", inbox.First(m => m.Uid == 1).Flags)}");
    Console.WriteLine($"marked: {await Counts()}");
    return 0;
}

Console.WriteLine($"EXPUNGE: {await Command("INBOX", "EXPUNGE")}");
Console.WriteLine($"filed: {await Counts()}");
if (mode == "expunge")
{
    Console.WriteLine($"STATUS: {await Command("", "STATUS INBOX (MESSAGES UIDNEXT)")}");
}

return 0;
