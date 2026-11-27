// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app's password reset mail against an in-process surl: the app sends three mails, and
// the test waits for each delivery, then checks recipient, subject and link in the stored copy.
// Run: dotnet run check-reset-mail.cs
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

// SMTP and IMAP on free ports over one mail store; each stored copy starts with Delivered-To.
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    MailDeliveredTo = true,
    RecordExchanges = true,
};
options.MailDomains.Add("example.com");
foreach (string name in new[] { "app", "ann", "bob", "carol", "dave" })
{
    options.Mailboxes.Add($"{name}@example.com:{name}pass");
}

await using var server = await SurlServer.StartAsync(options);
string smtp = $"{server.BoundUrls[0]}", imap = $"{server.BoundUrls[1]}";

// The app's three runs: the right mail to ann, a local link to carol, and To: dave sent to bob.
await Send("ann@example.com", ResetMail("ann@example.com", "https://app.example.com/reset/4f9c2a7e"));
await Send("carol@example.com", ResetMail("carol@example.com", "http://localhost:5000/reset/9b03d6e1"));
await Send("bob@example.com", ResetMail("dave@example.com", "https://app.example.com/reset/c71e0a52"));

// The test. Recipient and subject: the first copy in ann's INBOX, once it is there.
string ann = await Delivered("ann@example.com", TimeSpan.FromSeconds(10));
Console.WriteLine($"ann: {Fields(ann, "Delivered-To", "Subject")}");
Console.WriteLine($"ann's link: {Link(ann)}");
Console.WriteLine($"search in ann's INBOX: {await Search("ann@example.com")}");

// The local link: delivered, but the search for the right link finds nothing.
string carol = await Delivered("carol@example.com", TimeSpan.FromSeconds(10));
Console.WriteLine($"search in carol's INBOX: {await Search("carol@example.com")}");
Console.WriteLine($"carol's link: {Link(carol)}");

// The wrong envelope: nothing reaches dave, so a two-second wait ends; bob has the copy.
try
{
    await Delivered("dave@example.com", TimeSpan.FromSeconds(2));
    Console.WriteLine("dave: delivered");
}
catch (TimeoutException)
{
    Console.WriteLine("dave: TimeoutException after 2 s");
}

string bob = await Delivered("bob@example.com", TimeSpan.FromSeconds(10));
Console.WriteLine($"bob: {Fields(bob, "Delivered-To", "To")}");
return 0;

static byte[] ResetMail(string to, string link) => Encoding.ASCII.GetBytes(
    $"From: app@example.com\r\nTo: {to}\r\nSubject: Reset your password\r\n\r\n" +
    $"Someone asked to reset the password for {to}.\r\nOpen this link to choose a new one:\r\n{link}\r\n");

// The app's send through Curl.Api: the envelope recipient is the one that counts.
async Task Send(string recipient, byte[] message)
{
    await using CurlClient client = CurlClient.Create();
    await client.SendAsync(new CurlRequest(HttpMethod.Put, smtp + "app.example.com")
    {
        Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [recipient] },
        Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
        Body = message,
    }, CancellationToken.None);
}

// Waits, within the bound, for the first delivery to owner's mailbox, then reads that copy from the store.
async Task<string> Delivered(string owner, TimeSpan bound)
{
    await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = owner }, bound);
    SurlMailMessage message = await server.Mail.ReadMessageAsync(owner, "INBOX", 1);
    return Encoding.ASCII.GetString(message.Bytes.Span);
}

// The named header fields, in the order the message has them.
static string Fields(string message, params string[] names) => string.Join(" | ",
    message[..message.IndexOf("\r\n\r\n")].Split("\r\n").Where(l => names.Any(n => l.StartsWith(n + ": "))));

// The first reset link in the message, whatever its scheme and host.
static string Link(string message) => Regex.Match(message, @"https?://\S+/reset/[0-9a-f]+").Value;

// Recipient, subject and link in one IMAP SEARCH, as the owner.
async Task<string> Search(string owner)
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get,
        imap + $"INBOX?TO%20{owner}%20SUBJECT%20%22Reset%20your%20password%22%20BODY%20https://app.example.com/reset/")
    {
        Authentication = new CurlAuthenticationOptions { UserName = owner, Password = owner[..owner.IndexOf('@')] + "pass" },
    }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}
