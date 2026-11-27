// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An unread badge, checked two ways: the client's count over IMAP with Curl.Api, and the server's
// own count from Surl.Api, as a client reads messages and new mail comes in.
// Run: dotnet run unread-counts.cs [-- <case>]
//   (no argument)  three messages; counts after each read and after a fourth arrives
//   status         try to send IMAP STATUS through Curl.Api
//   no-folder      count a folder ann does not have
//   unknown-owner  ask the store about an account that does not exist
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "counts";

// IMAP on a free port, example.com, one mailbox.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
string imap = $"{server.BoundUrls[0]}";

// Orders put straight into ann's INBOX; DeliverAsync returns once each is stored.
async Task Deliver(int n) => await server.Mail.DeliverAsync("ann@example.com",
    Encoding.ASCII.GetBytes($"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {n} shipped\r\n\r\nOn its way.\r\n"));
for (int n = 1; n <= 3; n++)
{
    await Deliver(n);
}

await using CurlClient client = CurlClient.Create();
var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };

// One IMAP request through Curl.Api; the body is the answer.
async Task<string> Imap(string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, imap + path) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

if (mode == "status")
{
    // curl -X "STATUS INBOX (MESSAGES UNSEEN)" has no Curl.Api spelling: the method is one token.
    try
    {
        _ = new CurlRequest(new HttpMethod("STATUS INBOX (MESSAGES UNSEEN)"), imap) { Authentication = login };
    }
    catch (FormatException refused)
    {
        Console.WriteLine($"{refused.GetType().Name}: {refused.Message}");
    }

    return 1;
}

if (mode == "no-folder")
{
    try
    {
        await Imap("Archive?UNSEEN");
        return 0;
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"Archive: curl error {(int)failed.Code}, {failed.Message}");
        return 1;
    }
}

if (mode == "unknown-owner")
{
    try
    {
        await server.Mail.ListMailboxesAsync("nobody@example.com");
        return 0;
    }
    catch (SurlNotFoundException missing)
    {
        Console.WriteLine($"{missing.GetType().Name}: {missing.Message}");
        return 1;
    }
}

// The client's count: IMAP SEARCH UNSEEN, a query in the URL, answers "* SEARCH" and the numbers.
async Task<string> UnseenOverImap() => await Imap("INBOX?UNSEEN");

// The server's count, read with no client and setting no flag.
async Task<string> InStore()
{
    SurlMailboxInfo inbox = (await server.Mail.ListMailboxesAsync("ann@example.com")).Single(m => m.Name == "INBOX");
    return $"{inbox.MessageCount} messages, {inbox.UnseenCount} unseen";
}

async Task Report(string when) => Console.WriteLine($"{when}: IMAP {await UnseenOverImap()}; store {await InStore()}");

await Report("before");

// The client reads message 1 in full, which sets its \Seen flag.
await Imap("INBOX;UID=1");
await Report("after reading UID 1");

// The client reads only message 2's Subject header: curl asks for BODY[...], so that is a read too.
Console.WriteLine($"UID 2 header: {await Imap("INBOX;UID=2;SECTION=HEADER.FIELDS%20(SUBJECT)")}");
await Report("after reading UID 2's header");

// The test reads message 3 from the store: ReadMessageAsync never sets \Seen.
SurlMailMessage peeked = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", 3);
Console.WriteLine($"ReadMessageAsync UID 3: {peeked.Bytes.Length} bytes");
await Report("after ReadMessageAsync UID 3");

// New mail raises both counts.
await Deliver(4);
await Report("after a fourth delivery");
Console.WriteLine($"same client, ALL: {await Imap("INBOX?ALL")}");
await using (CurlClient fresh = CurlClient.Create())
{
    CurlResponse response = await fresh.SendAsync(new CurlRequest(HttpMethod.Get, imap + "INBOX?UNSEEN") { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    Console.WriteLine($"new client, UNSEEN: {(await new StreamReader(body).ReadToEndAsync()).Trim()}");
}

return 0;
