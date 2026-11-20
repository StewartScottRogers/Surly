// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app that mails support@ and alerts@: an in-process Surl server forwards both
// through aliases, the app sends with Curl.Api, and the test reads each target mailbox's store.
// Run: dotnet run route-mail.cs [-- <case>]
//   (no argument)  the app mails support@ and alerts@; the test passes
//   wrong-address  the app mails alert@ instead of alerts@
//   seed           the test delivers through the alias itself, with no SMTP
//   typo-target    the alerts@ alias names opps@, which has no mailbox
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "right";

// SMTP for the app, serving example.com: four mailboxes and two aliases.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
foreach (string name in new[] { "app", "ann", "bob", "ops" })
{
    options.Mailboxes.Add($"{name}@example.com:{name}pass");
}

options.MailAliases.Add("support@example.com=ann@example.com,bob@example.com");
options.MailAliases.Add(mode == "typo-target" ? "alerts@example.com=opps@example.com" : "alerts@example.com=ops@example.com");

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"refused: {refused.ExitCode}: {refused.Lines[0]}");
    return 2;
}

await using (server)
{
    // The app sends its two mails, or the test puts them in the store through the aliases.
    string alerts = mode == "wrong-address" ? "alert@example.com" : "alerts@example.com";
    if (mode == "seed")
    {
        await server.Mail.DeliverAsync("support@example.com", Message("support@example.com", "Cannot log in"), CancellationToken.None);
        await server.Mail.DeliverAsync(alerts, Message(alerts, "Disk full"), CancellationToken.None);
    }
    else
    {
        await using CurlClient app = CurlClient.Create();
        Console.WriteLine($"support@: SMTP {await Send(app, "support@example.com", "Cannot log in")}");
        Console.WriteLine($"{alerts.Split('@')[0]}@: SMTP {await Send(app, alerts, "Disk full")}");
    }

    // The test: the subjects in each target mailbox's INBOX, read from the store.
    var want = new Dictionary<string, string>
    {
        ["ann@example.com"] = "Cannot log in", ["bob@example.com"] = "Cannot log in", ["ops@example.com"] = "Disk full",
    };
    int wrong = 0;
    foreach ((string owner, string subject) in want)
    {
        List<string> got = await Subjects(owner);
        bool right = got.Count == 1 && got[0] == subject;
        Console.WriteLine(right ? $"{owner}: {subject}" : $"FAIL: {owner} has [{string.Join(", ", got)}], want [{subject}]");
        wrong += right ? 0 : 1;
    }

    return wrong == 0 ? 0 : 1;
}

// One mail from the app: Curl.Api sends SMTP with a stream body, logging in with CRAM-MD5.
async Task<int> Send(CurlClient client, string to, string subject)
{
    var mail = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
        Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [to] },
        Body = CurlContent.FromStream(new MemoryStream(Message(to, subject).ToArray())),
    };
    CurlResponse sent = await client.SendAsync(mail, CancellationToken.None);
    await sent.Body.DisposeAsync();
    return sent.StatusCode;
}

ReadOnlyMemory<byte> Message(string to, string subject) =>
    Encoding.UTF8.GetBytes($"From: app@example.com\r\nTo: {to}\r\nSubject: {subject}\r\n\r\nSent by the app.\r\n");

// Every message's Subject field in an account's INBOX, without a client and without setting \Seen.
async Task<List<string>> Subjects(string owner)
{
    var subjects = new List<string>();
    SurlMessagePage page = await server.Mail.ListMessagesAsync(owner, "INBOX", 0, 100, CancellationToken.None);
    foreach (SurlMessageSummary summary in page.Messages)
    {
        SurlMailMessage message = await server.Mail.ReadMessageAsync(owner, "INBOX", summary.Uid, CancellationToken.None);
        string text = Encoding.UTF8.GetString(message.Bytes.Span);
        subjects.Add(text.Split("\r\n").First(line => line.StartsWith("Subject: "))["Subject: ".Length..]);
    }

    return subjects;
}
