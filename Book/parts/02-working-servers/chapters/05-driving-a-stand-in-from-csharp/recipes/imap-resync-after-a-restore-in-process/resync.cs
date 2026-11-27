// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A fixture restores a snapshot of an in-process Surl server between tests. An IMAP client that
// caches messages by UID keeps the UIDVALIDITY it last saw; when the restore changes it, the client
// drops its cache and reads the mailbox again instead of trusting UIDs that now name other mail.
// Run: dotnet run resync.cs [-- <case>]
//   (no argument)  two tests with a restore between them; the client checks UIDVALIDITY
//   naive          the same two tests; the client ignores UIDVALIDITY and fetches only new UIDs
//   status         STATUS before and after a restore, and the store's own UidValidity
//   unchanged      a restore with nothing changed since the snapshot
//   reset          STATUS after ResetAsync instead of a restore
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "resync";

// The fixture: IMAP on a free port, Ann's mailbox seeded with two messages, then a snapshot.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using SurlServer server = await SurlServer.StartAsync(options);
await Deliver("Welcome");
await Deliver("Invoice 1042");
SurlSnapshot clean = await server.SnapshotAsync();

await using CurlClient curl = CurlClient.Create();
string imap = $"{server.BoundUrl}";

switch (mode)
{
    case "status":
        Console.WriteLine($"before: {await Imap("", "STATUS INBOX (UIDVALIDITY UIDNEXT)")}");
        await Deliver("Reset your password");
        await server.RestoreAsync(clean);
        Console.WriteLine($"after:  {await Imap("", "STATUS INBOX (UIDVALIDITY UIDNEXT)")}");
        var inbox = (await server.Mail.ListMailboxesAsync("ann@example.com")).Single(box => box.Name == "INBOX");
        Console.WriteLine($"store:  UidValidity {inbox.UidValidity}");
        return 0;
    case "unchanged":
        string first = await Imap("", "STATUS INBOX (UIDVALIDITY)");
        await server.RestoreAsync(clean);
        Console.WriteLine(first == await Imap("", "STATUS INBOX (UIDVALIDITY)") ? "UIDVALIDITY kept" : "UIDVALIDITY changed");
        return 0;
    case "reset":
        string before = await Imap("", "STATUS INBOX (UIDVALIDITY)");
        await server.ResetAsync();
        string after = await Imap("", "STATUS INBOX (UIDVALIDITY UIDNEXT MESSAGES)");
        Console.WriteLine($"{(UidValidity(before) == UidValidity(after) ? "UIDVALIDITY kept" : "UIDVALIDITY changed")}, {after[after.IndexOf("UIDNEXT")..^1]}");
        return 0;
}

// The client's cache: UIDVALIDITY, and each message's Subject by UID.
long? validity = null;
var cache = new SortedDictionary<long, string>();

// Test 1: the app's mail arrives; the client syncs.
await Deliver("Reset your password");
await Sync();
Show("after test 1");

// Between tests the fixture restores the snapshot. Test 2: different mail arrives; the client syncs.
await server.RestoreAsync(clean);
await Deliver("Your order has shipped");
await Sync();
Show("after test 2");
return cache.GetValueOrDefault(3) == "Subject: Your order has shipped" ? 0 : 1;

// Sync: if UIDVALIDITY changed, drop the cache and read every UID; otherwise read only UIDs above the last.
async Task Sync()
{
    long now = UidValidity(await Imap("", "STATUS INBOX (UIDVALIDITY)"));
    long from = 1;
    if (mode == "naive" || validity == now)
    {
        from = cache.Count == 0 ? 1 : cache.Keys.Last() + 1;
    }
    else
    {
        if (validity is not null)
        {
            Console.WriteLine($"UIDVALIDITY changed: {validity} to {now}; cache dropped");
        }

        cache.Clear();
    }

    validity = now;
    string found = await Imap("INBOX", $"UID SEARCH UID {from}:*");
    foreach (long uid in found.Split(' ').Skip(2).Select(long.Parse).Where(uid => uid >= from))
    {
        cache[uid] = await Imap($"INBOX;UID={uid};SECTION=HEADER.FIELDS%20(SUBJECT)", null);
    }
}

void Show(string when)
{
    Console.WriteLine($"{when}:");
    foreach ((long uid, string subject) in cache)
    {
        Console.WriteLine($"  UID {uid} {subject}");
    }
}

async Task Deliver(string subject) => await server.Mail.DeliverAsync("ann@example.com",
    Encoding.ASCII.GetBytes($"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nHello.\r\n"));

// One IMAP request as Ann with Curl.Api: a fetch by URL, or a command of its own. Answers the first line.
async Task<string> Imap(string path, string? command)
{
    CurlResponse response = await curl.SendAsync(new CurlRequest(HttpMethod.Get, imap + path)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" },
        CustomRequest = command,
    }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body, Encoding.ASCII).ReadToEndAsync()).Split("\r\n")[0];
}

static long UidValidity(string status) => long.Parse(Regex.Match(status, @"UIDVALIDITY (\d+)").Groups[1].Value);
