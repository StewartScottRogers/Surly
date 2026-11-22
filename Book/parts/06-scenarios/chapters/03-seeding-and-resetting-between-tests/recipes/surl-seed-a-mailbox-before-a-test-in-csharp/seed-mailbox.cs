// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds ten messages into a mailbox with Surl.Api before the client connects, lets the client
// read one over IMAP and delete one over POP3 with Curl.Api, and checks what it marked.
// Run: dotnet run seed-mailbox.cs [-- <address>]
//   (no argument)  seed ann@example.com and run the client (the default)
//   <address>      seed another address instead, such as bob@example.com
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Surl.Api;

string to = args.Length > 0 ? args[0] : "ann@example.com";

// The server: SMTP, IMAP and POP3 over one mail store, with one mailbox.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "pop3://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:secret");
await using SurlServer server = await SurlServer.StartAsync(options);

// The seed: ten messages, delivered as SMTP would deliver them, before any client connects.
try
{
    for (int i = 1; i <= 10; i++)
    {
        await server.Mail.DeliverAsync(to, System.Text.Encoding.UTF8.GetBytes(
            $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order {i:00}\r\n\r\nOrder {i:00} is ready.\r\n"));
    }
}
catch (SurlSeedRefusedException refused)
{
    Console.WriteLine($"seed refused: {refused.Store} {refused.Refusal}");
    return 1;
}

SurlMailboxInfo inbox = await Inbox();
Console.WriteLine($"seeded: {inbox.MessageCount} messages, {inbox.UnseenCount} unseen");

// The client under test: it reads UID 3 over IMAP and deletes message 1 over POP3.
var ann = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "secret" };
await using (CurlClient client = CurlClient.Create())
{
    CurlResponse read = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrls[1]}INBOX;UID=3") { Authentication = ann }, CancellationToken.None);
    await using (Stream body = read.Body)
    {
        string message = await new StreamReader(body).ReadToEndAsync();
        Console.WriteLine($"the client read: {message.Split("\r\n").First(l => l.StartsWith("Subject:"))}");
    }

    CurlResponse deleted = await client.SendAsync(new CurlRequest(new HttpMethod("DELE"), $"{server.BoundUrls[2]}1") { Authentication = ann }, CancellationToken.None);
    await deleted.Body.DisposeAsync();
    Console.WriteLine("the client sent DELE 1");
    Console.WriteLine($"before the client closed: {(await Inbox()).MessageCount} messages");
}

// After: the store as the client left it, read from the server's side. The server commits the DELE
// when it sees the POP3 session end, a moment after the client closes, so the test reads again until
// the count drops, for at most 10 s.
int held = inbox.MessageCount;
inbox = await Inbox();
DateTime deadline = DateTime.UtcNow.AddSeconds(10);
while (inbox.MessageCount >= held && DateTime.UtcNow < deadline)
{
    await Task.Delay(100);
    inbox = await Inbox();
}

Console.WriteLine($"after: {inbox.MessageCount} messages, {inbox.UnseenCount} unseen");
IReadOnlyList<SurlMessageSummary> left = (await server.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages;
Console.WriteLine($"seen: UID {string.Join(" ", left.Where(m => m.Flags.Contains(@"\Seen")).Select(m => m.Uid))}");
Console.WriteLine($"left: UID {string.Join(" ", left.Select(m => m.Uid))}");
return 0;

async Task<SurlMailboxInfo> Inbox() => (await server.Mail.ListMailboxesAsync("ann@example.com")).Single(m => m.Name == "INBOX");
