// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a sync client that fetches only new mail: it remembers the last UID it fetched, asks
// IMAP for the UIDs above it with UID SEARCH through Curl.Api, and fetches those. Mail is delivered
// between syncs, and Surl.Api's record shows which messages each sync fetched.
// Run: dotnet run sync.cs
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

// IMAP on a free port, example.com, one mailbox; every exchange recorded.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" }, RecordExchanges = true };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
string imap = $"{server.BoundUrls[0]}";
var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };
long lastSequence = 0;

async Task Deliver(int n) => await server.Mail.DeliverAsync("ann@example.com",
    Encoding.ASCII.GetBytes($"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {n} shipped\r\n\r\nOn its way.\r\n"));

async Task<string> Read(CurlClient client, string path, string? command = null)
{
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, imap + path) { Authentication = login, CustomRequest = command }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// The sync client: one connection per sync, so it selects INBOX afresh and sees what came since.
async Task<long> Sync(string name, long lastUid)
{
    int before = server.Exchanges.Count;
    string answer;
    var fetched = new List<long>();
    await using (CurlClient client = CurlClient.Create())
    {
        answer = await Read(client, "INBOX", $"UID SEARCH UID {lastUid + 1}:*");
        // * is the largest UID in use, so the answer can hold one already fetched: keep only those above lastUid.
        foreach (long uid in answer.Split(' ').Skip(2).Select(long.Parse).Where(u => u > lastUid))
        {
            await Read(client, $"INBOX;UID={uid}");
            fetched.Add(uid);
        }
    }

    // The test: once surl has ended every exchange it started (each wait at most 10 s), what did the sync ask it to fetch?
    while (server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).Count
        < server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted }).Count)
    {
        SurlEvent ended = await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
        lastSequence = ended.Sequence;
    }

    string asked = string.Join(" | ", server.Exchanges.Skip(before)
        .SelectMany(x => Encoding.ASCII.GetString(x.Received).Split("\r\n"))
        .Where(l => l.Contains(" UID FETCH ")).Select(l => l[(l.IndexOf(' ') + 1)..]));
    long last = fetched.Count > 0 ? fetched.Max() : lastUid;
    Console.WriteLine($"{name} (after UID {lastUid}): {answer}; fetched {(fetched.Count > 0 ? string.Join(" ", fetched) : "none")}; last UID {last}");
    Console.WriteLine($"{name}, surl received: {(asked.Length > 0 ? asked : "no UID FETCH")}");
    return last;
}

await Deliver(1);
await Deliver(2);
long seen = await Sync("sync 1", 0);

// Two more orders between syncs. The store lists what is above the last UID, as the test expects it.
await Deliver(3);
await Deliver(4);
SurlMessagePage newer = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX", seen, 100);
Console.WriteLine($"store above UID {seen}: {string.Join(" ", newer.Messages.Select(m => m.Uid))}");
seen = await Sync("sync 2", seen);
seen = await Sync("sync 3", seen);

// UIDNEXT and UIDVALIDITY: STATUS through CustomRequest, and the store's own UidValidity.
await using (CurlClient client = CurlClient.Create())
{
    string status = await Read(client, "", "STATUS INBOX (UIDNEXT UIDVALIDITY)");
    uint validity = (await server.Mail.ListMailboxesAsync("ann@example.com")).Single(m => m.Name == "INBOX").UidValidity;
    Console.WriteLine($"STATUS: UIDNEXT {status.Split(' ')[4]}; UIDVALIDITY equals the store's: {status.Contains($"UIDVALIDITY {validity})")}");
}

// Another client deletes message 1; a fifth order arrives. UIDs are not reused; message numbers shift.
await using (CurlClient client = CurlClient.Create())
{
    await Read(client, "INBOX", @"STORE 1 +FLAGS (\Deleted)");
    Console.WriteLine($"EXPUNGE: {await Read(client, "INBOX", "EXPUNGE")}");
}

await Deliver(5);
seen = await Sync("sync 4", seen);
await using (CurlClient client = CurlClient.Create())
{
    Console.WriteLine($"URL query INBOX?UID%205:*: {await Read(client, "INBOX?UID%205:*")}");
    try
    {
        await Read(client, "INBOX;UID=1");
    }
    catch (CurlException gone)
    {
        Console.WriteLine($"UID 1: CurlException {gone.Code} ({(int)gone.Code})");
    }
}

return 0;
