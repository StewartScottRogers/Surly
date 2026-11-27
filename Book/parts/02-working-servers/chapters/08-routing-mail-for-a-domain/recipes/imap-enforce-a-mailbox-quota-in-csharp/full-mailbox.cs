// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a client against a full mailbox: ann's mailboxes hold 1k, two newsletters fill most of
// it, and the next send is refused. Curl.Api reports the refusal; Surl.Api's record and store explain it.
// Run: dotnet run full-mailbox.cs [-- zero]
//   (no argument)  fill the mailbox, send again, then find what frees room
//   zero           start a server whose quota is 0
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "fill";

// SMTP and IMAP on free ports; ann's mailboxes hold at most 1k, bob has no quota.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add(mode == "zero" ? "ann@example.com:annpass;quota=0" : "ann@example.com:annpass;quota=1k");
options.Mailboxes.Add("bob@example.com:bobpass");
options.Mailboxes.Add("shop@example.com:shoppass");

if (mode == "zero")
{
    try
    {
        await using SurlServer refused = await SurlServer.StartAsync(options);
        return 0;
    }
    catch (SurlServerStartException failed)
    {
        Console.WriteLine($"{failed.GetType().Name}: {failed.ExitCode}, {failed.Lines[0]}");
        return 1;
    }
}

await using var server = await SurlServer.StartAsync(options);
string smtp = $"{server.BoundUrls[0]}", imap = $"{server.BoundUrls[1]}";
byte[] newsletter = Encoding.ASCII.GetBytes(
    "From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Our autumn newsletter\r\n\r\n" +
    "New this autumn: wool coats in five colours, boots with a lighter sole,\r\n" +
    "and scarves woven from the same yarn as the coats. Every order over\r\n" +
    "fifty pounds ships free until the end of the month, and returns stay\r\n" +
    "free for thirty days. See you in store.\r\n");
long lastSequence = 0;

// The app's send, as the shop: a fresh CurlClient, one SMTP exchange, then a bounded wait until surl has ended it.
async Task<string> Send(params string[] recipients)
{
    string outcome;
    await using (CurlClient client = CurlClient.Create())
    {
        try
        {
            CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, smtp + "example.com")
            {
                Mail = new CurlMailOptions { MailFrom = "shop@example.com", MailRecipients = recipients },
                Authentication = new CurlAuthenticationOptions { UserName = "shop@example.com", Password = "shoppass" },
                Body = newsletter,
            }, CancellationToken.None);
            outcome = $"{sent.StatusCode}";
        }
        catch (CurlException refused)
        {
            outcome = $"CurlException {refused.Code} ({(int)refused.Code}): {refused.Message}";
        }
    }

    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "smtp", AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
    return outcome;
}

// One IMAP command on INBOX, as ann, on a fresh connection.
async Task<string> Imap(string command)
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, imap + "INBOX")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" },
        CustomRequest = command,
    }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

async Task<int> Count(string owner) => (await server.Mail.ListMailboxesAsync(owner)).Single(m => m.Name == "INBOX").MessageCount;

Console.WriteLine($"send 1: {await Send("ann@example.com")}");
Console.WriteLine($"send 2: {await Send("ann@example.com")}");
Console.WriteLine($"send 3: {await Send("ann@example.com")}");

// What surl answered after the RCPT, from the recorded exchange: each line it sent.
SurlExchangeRecord third = server.Exchanges[^1];
string[] replies = third.Chunks.Where(c => c.Direction == SurlExchangeDirection.Sent)
    .Select(c => Encoding.ASCII.GetString(c.Bytes.Span).TrimEnd()).ToArray();
Console.WriteLine($"replies from RCPT on: {string.Join(" | ", replies.SkipWhile(r => !r.StartsWith("250 2.1.5")).TakeWhile(r => !r.StartsWith("221")))}");
Console.WriteLine($"INBOX: {await Count("ann@example.com")} messages");
SurlMessagePage page = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX", 0, 100);
Console.WriteLine($"stored sizes: {string.Join(", ", page.Messages.Select(m => m.Size))} of {newsletter.Length} sent");

// The store's own delivery obeys the quota too.
try
{
    await server.Mail.DeliverAsync("ann@example.com", newsletter);
    Console.WriteLine("DeliverAsync: delivered");
}
catch (SurlSeedRefusedException refused)
{
    Console.WriteLine($"DeliverAsync: {refused.GetType().Name} {refused.Refusal}");
}

// One full mailbox holds the message back from every recipient; bob alone has room.
Console.WriteLine($"bob and ann: {await Send("bob@example.com", "ann@example.com")}; bob's INBOX: {await Count("bob@example.com")} messages");
Console.WriteLine($"bob alone: {await Send("bob@example.com")}");

// \Deleted still counts; EXPUNGE frees the room.
Console.WriteLine($"STORE 1 +FLAGS (\\Deleted): {await Imap(@"STORE 1 +FLAGS (\Deleted)")}");
Console.WriteLine($"send with UID 1 deleted: {await Send("ann@example.com")}");
Console.WriteLine($"EXPUNGE: {await Imap("EXPUNGE")}");
Console.WriteLine($"send after EXPUNGE: {await Send("ann@example.com")}");

// The log names the quota at start.
await server.StopAsync();
Console.WriteLine($"log: {log.ToString().Split('\n').Select(l => l.TrimEnd()).Single(l => l.StartsWith("* Mailbox ann@example.com:"))}");
return 0;
