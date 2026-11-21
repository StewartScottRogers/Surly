// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that fails when the app mails outside its own domain: an in-process Surl server for
// example.com refuses every other recipient, and the app's SmtpClient throws.
// Run: dotnet run stray-recipient.cs [-- <case>]
//   (no argument)  the app mails carol@elsewhere.org
//   own-domain     the app mails ann@example.com
//   mixed          one mail to ann@example.com and carol@elsewhere.org
//   no-domain      the app mails carol@elsewhere.org, and the server has no mail domain
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Net;
using System.Net.Mail;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "stray";

// The server: SMTP on a free port, mail for example.com only, a mailbox for the app and one for ann.
// no-domain shows the same test against a server with no mail domain, only an account.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, AllowPlaintextAuthentication = true };
if (mode == "no-domain")
{
    options.Accounts.Add(new SurlAccount("app@example.com", "apppass"));
}
else
{
    options.MailDomains.Add("example.com");
    options.Mailboxes.Add("app@example.com:apppass");
    options.Mailboxes.Add("ann@example.com:annpass");
}

await using var server = await SurlServer.StartAsync(options);
Uri smtp = server.BoundUrls[0];

// The app under test: SmtpClient, logged in, mailing the recipients the case chooses.
string recipients = mode switch
{
    "own-domain" => "ann@example.com",
    "mixed" => "ann@example.com,carol@elsewhere.org",
    _ => "carol@elsewhere.org",
};
int result = 0;
using (var app = new SmtpClient(smtp.Host, smtp.Port) { Credentials = new NetworkCredential("app@example.com", "apppass") })
{
    try
    {
        app.Send("app@example.com", recipients, "Welcome", "Thanks for signing up.");
        Console.WriteLine($"sent to {recipients}");
    }
    catch (SmtpFailedRecipientException refused)
    {
        // The test fails here: the server named the recipient it would not take.
        Console.WriteLine($"FAIL: refused {refused.FailedRecipient}: {refused.Message}");
        result = 1;
    }
}

// What the server stored for ann, read with no client.
if (mode != "no-domain")
{
    SurlMailboxInfo inbox = (await server.Mail.ListMailboxesAsync("ann@example.com")).Single();
    Console.WriteLine($"messages for ann: {inbox.MessageCount}");
}

return result;
