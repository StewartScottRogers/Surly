// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that an app reports the one recipient whose address the server refuses, and still delivers to
// the others: an in-process Surl server fails the RCPT for one address, the app sends one notice to
// three people with SmtpClient, and the test reads what was reported and what each mailbox holds.
// Run: dotnet run fail-one.cs [-- <case>]
//   (no argument)  bob's address is refused 550; ann and cy are accepted
//   two            bob's and cy's are refused
//   temporary      bob's is refused 451, try later
//   only           the notice goes to bob alone
//   unaffected     the notice goes to ann and cy, and the rule never acts
//   log            the server's log lines for the refusal and the stored message
//   curl           the same notice sent with Curl.Api, which abandons it, then with MailRecipientAllowFails
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "one";

// SMTP on a free port for example.com, a mailbox for the app and one for each person.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, AllowPlaintextAuthentication = true, RecordExchanges = true, Log = log, LogLevel = LogLevel.Verbose };
options.MailDomains.Add("example.com");
foreach (string name in new[] { "app", "ann", "bob", "cy" })
{
    options.Mailboxes.Add($"{name}@example.com:{name}pass");
}

// The fault: RCPT for bob's address (the path key of the rule is the recipient) answered with an error.
string reply = mode == "temporary" ? "451 4.3.0 try later" : "550 5.1.1 mailbox unavailable";
foreach (string refused in mode == "two" ? new[] { "bob@example.com", "cy@example.com" } : new[] { "bob@example.com" })
{
    options.Faults.Add(SurlFault.ProtocolError(reply) with { Scheme = "smtp", Command = "RCPT", Path = refused });
}

await using SurlServer server = await SurlServer.StartAsync(options);

// Who the notice goes to, and how many deliveries the test waits for.
string[] to = mode switch
{
    "only" => ["bob@example.com"],
    "unaffected" => ["ann@example.com", "cy@example.com"],
    "two" => ["ann@example.com", "bob@example.com", "cy@example.com"],
    _ => ["ann@example.com", "bob@example.com", "cy@example.com"],
};
int expected = mode switch { "only" or "temporary" or "curl" => 0, "two" => 1, _ => to.Count(address => address != "bob@example.com") };

// Delivery is asynchronous: every delivery is an event, and the wait for them has a bound.
SemaphoreSlim arrived = new(0);
using IDisposable watching = server.OnEvent(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    (e, cancellationToken) =>
    {
        arrived.Release();
        return ValueTask.CompletedTask;
    });

async Task<bool> Delivered(int count)
{
    for (int i = 0; i < count; i++)
    {
        if (!await arrived.WaitAsync(TimeSpan.FromSeconds(10)))
        {
            return false;
        }
    }

    return true;
}

if (mode == "curl")
{
    await using CurlClient client = CurlClient.Create();
    foreach (bool allowFails in new[] { false, true })
    {
        var send = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = to, MailRecipientAllowFails = allowFails },
            Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(Notice(to)))),
        };
        try
        {
            CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
            await sent.Body.DisposeAsync();
            Console.WriteLine($"allow fails {allowFails}: SMTP {sent.StatusCode}");
            if (!await Delivered(2))
            {
                Console.WriteLine("no delivery within 10 s");
                return 1;
            }
        }
        catch (CurlException refused)
        {
            Console.WriteLine($"allow fails {allowFails}: curl error {(int)refused.Code} {refused.Code}: {refused.Message}");
            await Report();
        }
    }

    await Report();
    return 0;
}

// The app: SmtpClient with the notice for everyone, logged in as app@example.com.
using var app = new SmtpClient(server.BoundUrls[0].Host, server.BoundUrls[0].Port)
{
    Credentials = new NetworkCredential("app@example.com", "apppass"),
};
var notice = new MailMessage { From = new MailAddress("app@example.com"), Subject = "Maintenance on Saturday", Body = "The system is down from 02:00 to 04:00." };
foreach (string address in to)
{
    notice.To.Add(address);
}

try
{
    app.Send(notice);
    Console.WriteLine("sent to everyone");
}
catch (SmtpFailedRecipientsException failed)
{
    foreach (SmtpFailedRecipientException each in failed.InnerExceptions)
    {
        Console.WriteLine($"reported: {each.FailedRecipient} {each.StatusCode}");
    }
}
catch (SmtpFailedRecipientException failed)
{
    Console.WriteLine($"reported: {failed.FailedRecipient} {failed.StatusCode}: {failed.Message}");
}
catch (SmtpException failed)
{
    Console.WriteLine($"reported: {failed.GetType().Name} {failed.StatusCode}: {failed.Message}");
}

if (!await Delivered(expected))
{
    Console.WriteLine("no delivery within 10 s");
    return 1;
}

await Report();
if (mode == "log")
{
    string text = log.ToString();
    Console.WriteLine($"refusals logged: {Regex.Matches(text, "RCPT answered with the chosen error of fault rule f\\d; nothing was stored.").Count}");
    Console.WriteLine($"stored logged: {Regex.Matches(text, "Message stored: \\d+ bytes for 2 recipients").Count}");
}

if (mode == "unaffected")
{
    Console.WriteLine($"fault events: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count}");
}

return 0;

// What each person's mailbox holds, read from the store with no client.
async Task Report()
{
    foreach (string owner in new[] { "ann@example.com", "bob@example.com", "cy@example.com" })
    {
        Console.WriteLine($"{owner} holds {(await server.Mail.ListMessagesAsync(owner, "INBOX")).Messages.Count}");
    }
}

string Notice(string[] recipients) =>
    $"From: app@example.com\r\nTo: {string.Join(", ", recipients)}\r\nSubject: Maintenance on Saturday\r\n\r\nThe system is down from 02:00 to 04:00.\r\n";
