// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that a mail client never uses POP3: ann's mailbox logs in over IMAP only, the POP3 login is
// refused through Curl.Api, and the server's record shows each refusal alike and no POP3 login accepted.
// Run: dotnet run imap-only.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

// SMTP, IMAP and POP3 on free ports over one mail store; ann logs in over IMAP alone, bob everywhere.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "pop3://127.0.0.1:0" },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass;protocols=imap");
options.Mailboxes.Add("bob@example.com:bobpass");
options.Mailboxes.Add("shop@example.com:shoppass");

await using var server = await SurlServer.StartAsync(options);
string smtp = $"{server.BoundUrls[0]}", imap = $"{server.BoundUrls[1]}", pop3 = $"{server.BoundUrls[2]}";
long lastSequence = 0;

// The shop's mail to ann over SMTP; the test waits, at most ten seconds, for its delivery.
await using (CurlClient client = CurlClient.Create())
{
    await client.SendAsync(new CurlRequest(HttpMethod.Put, smtp + "shop.example.com")
    {
        Mail = new CurlMailOptions { MailFrom = "shop@example.com", MailRecipients = ["ann@example.com"] },
        Authentication = Login("shop@example.com", "shoppass"),
        Body = "From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order 1042 shipped\r\n\r\nYour order is on its way.\r\n"u8.ToArray(),
    }, CancellationToken.None);
}

await server.WaitForEventAsync(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "ann@example.com" }, TimeSpan.FromSeconds(10));

// POP3 with ann's right password, then the line the server answered, from the recorded exchange.
Console.WriteLine($"ann over POP3: {await Pop3("ann@example.com", "annpass")}");
Console.WriteLine($"nobody over POP3: {await Pop3("nobody@example.com", "annpass")}");
Console.WriteLine($"bob, wrong password, over POP3: {await Pop3("bob@example.com", "wrong")}");

// IMAP reads the mail; a client that falls back from POP3 to IMAP gets it.
Console.WriteLine($"ann over IMAP: {await Read(imap + "INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)", "ann@example.com", "annpass")}");
string fallback;
try
{
    fallback = await Read(pop3, "ann@example.com", "annpass");
}
catch (CurlException refused) when (refused.Code == CurlCode.LoginDenied)
{
    fallback = await Read(imap + "INBOX?ALL", "ann@example.com", "annpass");
}

Console.WriteLine($"fall back to IMAP: {fallback}");

// The test's assertion: every POP3 login the server judged, and how many it accepted.
var pop3Logins = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged, Scheme = "pop3" }).Cast<LoginJudgedEvent>().ToList();
foreach (LoginJudgedEvent judged in pop3Logins)
{
    Console.WriteLine($"judged: {judged.Scheme} {judged.Mechanism} {judged.Account} IsAccepted={judged.IsAccepted}");
}

Console.WriteLine($"POP3 logins accepted: {pop3Logins.Count(e => e.IsAccepted)}");

// The log named the protocols at start.
await server.StopAsync();
Console.WriteLine($"log: {log.ToString().Split('\n').Select(l => l.TrimEnd()).Single(l => l.StartsWith("* Mailbox ann@example.com:"))}");
return 0;

static CurlAuthenticationOptions Login(string user, string password) => new() { UserName = user, Password = password };

// A POP3 listing on a fresh client; on a refusal, the exception and the server's last line, read once the exchange has ended.
async Task<string> Pop3(string user, string password)
{
    string outcome;
    try
    {
        outcome = await Read(pop3, user, password);
    }
    catch (CurlException refused)
    {
        outcome = $"CurlException {refused.Code} ({(int)refused.Code}): {refused.Message}";
    }

    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "pop3", AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
    SurlExchangeRecord exchange = server.Exchanges.Last(x => x.Scheme == "pop3");
    string last = exchange.Chunks.Where(c => c.Direction == SurlExchangeDirection.Sent)
        .Select(c => Encoding.ASCII.GetString(c.Bytes.Span).TrimEnd()).Last();
    return $"{outcome} | server: {last}";
}

static async Task<string> Read(string url, string user, string password)
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(
        new CurlRequest(HttpMethod.Get, url) { Authentication = Login(user, password) }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}
