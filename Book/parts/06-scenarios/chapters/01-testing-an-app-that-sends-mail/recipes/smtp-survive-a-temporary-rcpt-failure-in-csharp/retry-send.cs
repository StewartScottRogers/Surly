// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that the app's sender retries a temporary refusal: an in-process Surl server answers the
// first RCPT 451, the app's SmtpClient tries again, and the test counts what the server stored.
// Run: dotnet run retry-send.cs [-- <case>]
//   (no argument)  the first RCPT is answered 451; the sender retries temporary refusals
//   no-retry       the same server; the sender gives up on the first refusal
//   permanent      every RCPT is answered 550; the sender retries temporary refusals only
//   no-plaintext   the first case, with the server's default: no password in clear without TLS
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Net;
using System.Net.Mail;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "temporary";

// The server: SMTP on a free port for example.com, and one fault rule on RCPT.
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0" },
    RecordExchanges = true,
    AllowPlaintextAuthentication = mode != "no-plaintext",
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Faults.Add(mode == "permanent"
    ? SurlFault.ProtocolError("550 5.1.1 mailbox unavailable") with { Scheme = "smtp", Command = "RCPT" }
    : SurlFault.ProtocolError("451 4.3.0 try later") with { Scheme = "smtp", Command = "RCPT", Times = 1 });
await using var server = await SurlServer.StartAsync(options);
Uri smtp = server.BoundUrls[0];

// The app's sender: up to three tries, the next one only after a temporary (4xx) refusal.
int tries = mode == "no-retry" ? 1 : 3;
int result = 1;
using (var app = new SmtpClient(smtp.Host, smtp.Port) { Credentials = new NetworkCredential("app@example.com", "apppass") })
{
    for (int attempt = 1; attempt <= tries; attempt++)
    {
        try
        {
            app.Send("app@example.com", "ann@example.com", "Order 1042 has shipped", "Your order is on its way.");
            Console.WriteLine($"try {attempt}: sent");
            result = 0;
            break;
        }
        catch (SmtpException refused)
        {
            int code = (int)refused.StatusCode;
            Console.WriteLine($"try {attempt}: {refused.GetType().Name}, {code} {refused.StatusCode}: {refused.Message}");
            if (code >= 500)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}

// What the server did and stored, read with no client.
Console.WriteLine($"rule {server.Faults.List()[0].Id} remaining: {server.Faults.List()[0].Remaining?.ToString() ?? "no limit"}");
Console.WriteLine($"fault applied: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count}");
SurlMailboxInfo inbox = (await server.Mail.ListMailboxesAsync("ann@example.com")).Single();
Console.WriteLine($"messages for ann: {inbox.MessageCount}");
return result == 0 && inbox.MessageCount == 1 ? 0 : 1;
