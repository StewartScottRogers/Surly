// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test checks a mailbox holding hundreds of messages by walking it a page at a time with
// ListMessagesAsync: each page starts after the last UID seen, and HasMore says when to stop.
// Run: dotnet run page-mailbox.cs [-- <case>]
//   (no argument)  250 messages, pages of 100
//   exact          200 messages, pages of 100
//   arrives        250 messages; one more is delivered after the first page
//   deleted        250 messages; UID 10, already seen, is deleted over IMAP after the first page
//   limit          a page of 1001
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "walk";

// IMAP on a free port for example.com, with Ann's mailbox.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using SurlServer server = await SurlServer.StartAsync(options);

// The mailbox under test: one order notice per message, seeded straight into the store.
int count = mode == "exact" ? 200 : 250;
for (int order = 1; order <= count; order++)
{
    await server.Mail.DeliverAsync("ann@example.com", Notice(order), CancellationToken.None);
}

if (mode == "limit")
{
    try
    {
        await server.Mail.ListMessagesAsync("ann@example.com", "INBOX", 0, 1001, CancellationToken.None);
    }
    catch (SurlReadRefusedException refused)
    {
        Console.WriteLine($"{refused.GetType().Name}: {refused.Message}");
    }

    return 0;
}

// The walk: each page asks for the messages after the last UID seen, at most 100 of them.
var seen = new List<uint>();
long afterUid = 0;
int pages = 0;
while (true)
{
    SurlMessagePage page = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX", afterUid, 100, CancellationToken.None);
    pages++;
    Console.WriteLine($"page {pages}: {page.Messages.Count} messages, UIDs {page.Messages[0].Uid} to {page.Messages[^1].Uid}, HasMore {page.HasMore}");
    seen.AddRange(page.Messages.Select(m => m.Uid));
    if (!page.HasMore)
    {
        break;
    }

    afterUid = page.Messages[^1].Uid;
    if (pages == 1 && mode == "arrives")
    {
        await server.Mail.DeliverAsync("ann@example.com", Notice(count + 1), CancellationToken.None);
    }
    else if (pages == 1 && mode == "deleted")
    {
        await DeleteOverImap(10);
    }
}

bool once = seen.Distinct().Count() == seen.Count && seen.SequenceEqual(seen.Order());
Console.WriteLine($"{seen.Count} messages, {(once ? "each once, in UID order" : "with repeats or out of order")}");

if (mode == "deleted")
{
    // Counting by position instead: the 101st message is no longer UID 101.
    SurlMessagePage all = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX", 0, 1000, CancellationToken.None);
    Console.WriteLine($"by position, the 101st message is now UID {all.Messages[100].Uid}");
}
else
{
    // The last message seen, read back from the store.
    SurlMailMessage last = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", seen[^1], CancellationToken.None);
    Console.WriteLine(Encoding.ASCII.GetString(last.Bytes.Span).Split("\r\n").First(l => l.StartsWith("Subject: ")));
}

return 0;

static byte[] Notice(int order) => Encoding.ASCII.GetBytes(
    $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {order} shipped\r\n\r\nOrder {order} left the warehouse.\r\n");

// Ann's mail client deletes a message by UID over IMAP: flag it \Deleted, then expunge.
async Task DeleteOverImap(uint uid)
{
    await using CurlClient client = CurlClient.Create();
    var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };
    foreach (string command in new[] { $"UID STORE {uid} +FLAGS (\\Deleted)", "EXPUNGE" })
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}INBOX") { Authentication = login, CustomRequest = command }, CancellationToken.None);
        await response.Body.DisposeAsync();
    }
}
