// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of moving a mail client from POP3 to IMAP: one in-process server serves one mailbox over both,
// the old client and the new one read it, and the store and the recorded logins are read back.
// Run: dotnet run mail.cs [-- ids|flags|password|delete|cutover]
//   (no argument) the first message over POP3 and over IMAP, compared, and the logins recorded
//   ids           the POP3 listing, the IMAP search and the POP3 unique IDs
//   flags         what each client's read does to the unseen count
//   password      a wrong password over both
//   delete        the old client deletes a message
//   cutover       a mailbox whose logins are limited to SMTP and IMAP
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "move";
const string Ann = "ann@example.com";

// The server: one mail store served over POP3 and IMAP, one mailbox, two orders in it.
var options = new SurlServerOptions
{
    ListenUrls = { "pop3://127.0.0.1:0", "imap://127.0.0.1:0" },
    RecordExchanges = true,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add(which == "cutover" ? $"{Ann}:s3cret;protocols=smtp,imap" : $"{Ann}:s3cret");
await using SurlServer server = await SurlServer.StartAsync(options);
string pop3 = server.BoundUrls[0].ToString();
string imap = server.BoundUrls[1].ToString();
for (int n = 1; n <= 2; n++)
{
    await server.Mail.DeliverAsync(Ann, Encoding.ASCII.GetBytes($"From: shop@example.com\r\nTo: {Ann}\r\nSubject: Order {n}\r\n\r\norder {n}\r\n"));
}

switch (which)
{
    case "move":
        string? overPop3 = await Read("over pop3", HttpMethod.Get, $"{pop3}1");
        string? overImap = await Read("over imap", HttpMethod.Get, $"{imap}INBOX;UID=1");
        Console.WriteLine(overPop3 == overImap ? "the two copies are identical" : "the copies DIFFER");
        foreach (string scheme in new[] { "pop3", "imap" })
        {
            int logins = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>()
                .Count(l => l.Scheme == scheme && l.Account == Ann && l.IsAccepted);
            Console.WriteLine($"{scheme} logins recorded: {logins}");
        }

        break;
    case "ids":
        Console.Write(await Read(null, HttpMethod.Get, pop3));
        Console.Write(await Read(null, HttpMethod.Get, $"{imap}INBOX?ALL"));
        Console.Write(await Read(null, new HttpMethod("UIDL"), pop3));
        break;
    case "flags":
        await Unseen("at start");
        await Read(null, HttpMethod.Get, $"{imap}INBOX;UID=1");
        Console.Write($"IMAP search after an IMAP fetch of UID 1: {await Read(null, HttpMethod.Get, $"{imap}INBOX?UNSEEN")}");
        await Read(null, HttpMethod.Get, $"{pop3}2");
        Console.Write($"IMAP search after a POP3 retrieve of 2: {await Read(null, HttpMethod.Get, $"{imap}INBOX?UNSEEN")}");
        await Unseen("at the end");
        break;
    case "password":
        await Read("pop3", HttpMethod.Get, pop3, "wrong");
        await Read("imap", HttpMethod.Get, $"{imap}INBOX?ALL", "wrong");
        break;
    case "delete":
        await Read(null, new HttpMethod("DELE"), $"{pop3}1");
        await Unseen("after a POP3 DELE 1");
        await Read("IMAP fetch of UID 1", HttpMethod.Get, $"{imap}INBOX;UID=1");
        break;
    case "cutover":
        await Read("pop3", HttpMethod.Get, pop3);
        Console.Write($"imap: {await Read(null, HttpMethod.Get, $"{imap}INBOX?ALL")}");
        break;
}

return 0;

// One request through a client of its own, disposed at the end: a CurlClient keeps its connection, and
// a POP3 delete is committed when the session ends. The body as text, or null with the refusal.
async Task<string?> Read(string? label, HttpMethod method, string url, string password = "s3cret")
{
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(method, url)
        {
            Authentication = new CurlAuthenticationOptions { UserName = Ann, Password = password },
        }, CancellationToken.None);
        await using Stream body = response.Body;
        string text = (await new StreamReader(body).ReadToEndAsync()).Replace("\r\n", "\n");
        if (label is not null)
        {
            Console.WriteLine($"{label}: {text.Length} characters");
        }

        return text;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label ?? "refused"}: {(int)failure.Code} {failure.Code}");
        return null;
    }
}

// The store's count for INBOX, read without a protocol.
async Task Unseen(string when)
{
    SurlMailboxInfo inbox = (await server.Mail.ListMailboxesAsync(Ann)).Single(m => m.Name == "INBOX");
    Console.WriteLine($"INBOX {when}: {inbox.MessageCount} messages, {inbox.UnseenCount} unseen");
}
