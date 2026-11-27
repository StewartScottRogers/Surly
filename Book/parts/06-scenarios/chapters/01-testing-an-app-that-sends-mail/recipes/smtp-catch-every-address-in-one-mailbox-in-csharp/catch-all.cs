// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// The app mails generated addresses with SmtpClient; a Surl catch-all lands them in one mailbox,
// and the test counts that mailbox over POP3 with Curl.Api.
// Run: dotnet run catch-all.cs [-- <case>]
//   (no argument)  one mail to each of three generated addresses
//   one-message    one mail to all three addresses at once
//   no-catch-all   the same three mails to a server with no catch-all
//   other-domain   one mail to an address outside the mail domain
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Net;
using System.Net.Mail;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "each";

// The server: SMTP and POP3 on free ports, example.com, the app's mailbox and the catch-all's.
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0", "pop3://127.0.0.1:0" },
    AllowPlaintextAuthentication = true,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("catchall@example.com:catchpass");
if (mode != "no-catch-all")
{
    options.MailCatchAlls.Add("example.com=catchall@example.com");
}

await using var server = await SurlServer.StartAsync(options);
Uri smtp = server.BoundUrls[0];
string pop3 = $"{server.BoundUrls[1]}";

// The app: addresses it made up, mailed one message each, or one message for all.
string domain = mode == "other-domain" ? "example.org" : "example.com";
string[] addresses = [.. Enumerable.Range(1, mode == "other-domain" ? 1 : 3).Select(n => $"user-{n}@{domain}")];
using (var app = new SmtpClient(smtp.Host, smtp.Port) { Credentials = new NetworkCredential("app@example.com", "apppass") })
{
    try
    {
        if (mode == "one-message")
        {
            app.Send("app@example.com", string.Join(",", addresses), "You are invited", "Follow the link to join.");
        }
        else
        {
            foreach (string address in addresses)
            {
                app.Send("app@example.com", address, "You are invited", "Follow the link to join.");
            }
        }
    }
    catch (SmtpFailedRecipientException refused)
    {
        Console.WriteLine($"refused {refused.FailedRecipient}: {refused.Message}");
        return 1;
    }
}

Console.WriteLine($"mailed {addresses.Length} addresses");

// The test: list the catch-all's maildrop over POP3, one line per message, and count the lines.
string list;
await using (CurlClient client = CurlClient.Create())
{
    CurlRequest request = new(HttpMethod.Get, pop3)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "catchall@example.com", Password = "catchpass" },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    list = await new StreamReader(body).ReadToEndAsync();
}

int caught = list.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length;
Console.WriteLine($"caught over POP3: {caught}");

// The same count from the store itself, with no client.
SurlMailboxInfo inbox = (await server.Mail.ListMailboxesAsync("catchall@example.com")).Single();
Console.WriteLine($"caught in the store: {inbox.MessageCount}");
return caught == addresses.Length ? 0 : 1;
