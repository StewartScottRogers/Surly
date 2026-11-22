// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a mail client's run: the app reads two orders and marks the spam \Deleted over IMAP
// with Curl.Api; the test then reads every message's flags from Surl.Api's store.
// Run: dotnet run app-flags.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Surl.Api;

// IMAP on a free port, example.com, one mailbox.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
string imap = $"{server.BoundUrls[0]}";

// Four messages into ann's INBOX; DeliverAsync returns once each is stored.
foreach (string subject in new[] { "Order 1 shipped", "Order 2 shipped", "You have won a prize", "Our autumn newsletter" })
{
    await server.Mail.DeliverAsync("ann@example.com",
        Encoding.ASCII.GetBytes($"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nSee inside.\r\n"));
}

var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };

// One IMAP request on a fresh connection: a URL to fetch, or a command (CURLOPT_CUSTOMREQUEST) on INBOX.
async Task<string> Imap(string path, string? command = null)
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, imap + path) { Authentication = login, CustomRequest = command }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// The app's run: it reads both orders, reads the spam's subject and marks it \Deleted, and leaves the newsletter alone.
await Imap("INBOX;UID=1");
await Imap("INBOX;UID=2");
await Imap("INBOX;UID=3;SECTION=HEADER.FIELDS%20(SUBJECT)");
await Imap("INBOX", @"STORE 3 +FLAGS (\Deleted)");

// The test: every message's flags, from the store, which sets none.
async Task<string> Flags()
{
    SurlMessagePage page = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX", 0, 100);
    return string.Join("; ", page.Messages.Select(m => $"UID {m.Uid} ({string.Join(" ", m.Flags)})"));
}

Console.WriteLine($"after the app: {await Flags()}");
Console.WriteLine($"listed again: {await Flags()}");
Console.WriteLine($"IMAP UNSEEN: {await Imap("INBOX?UNSEEN")}");
Console.WriteLine($"IMAP DELETED: {await Imap("INBOX?DELETED")}");
Console.WriteLine($"IMAP SEEN NOT DELETED: {await Imap("INBOX?SEEN%20NOT%20DELETED")}");
SurlMailboxInfo inbox = (await server.Mail.ListMailboxesAsync("ann@example.com")).Single(m => m.Name == "INBOX");
Console.WriteLine($"store: {inbox.MessageCount} messages, {inbox.UnseenCount} unseen");

// Reading through the store sets no flag; fetching over IMAP to look is a read and sets \Seen.
SurlMailMessage peeked = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", 4);
Console.WriteLine($"ReadMessageAsync UID 4: {peeked.Bytes.Length} bytes, flags ({string.Join(" ", peeked.Summary.Flags)})");
await Imap("INBOX;UID=4");
Console.WriteLine($"after an IMAP fetch of UID 4: {await Flags()}");
Console.WriteLine($"STORE 4 -FLAGS (\\Seen): {await Imap("INBOX", @"STORE 4 -FLAGS (\Seen)")}");

// EXPUNGE removes what is marked \Deleted; numbers shift, UIDs do not.
Console.WriteLine($"EXPUNGE: {await Imap("INBOX", "EXPUNGE")}");
Console.WriteLine($"after EXPUNGE: {await Flags()}");
Console.WriteLine($"IMAP UID FETCH: {(await Imap("INBOX", "UID FETCH 1:* FLAGS")).ReplaceLineEndings(" | ")}");
return 0;
