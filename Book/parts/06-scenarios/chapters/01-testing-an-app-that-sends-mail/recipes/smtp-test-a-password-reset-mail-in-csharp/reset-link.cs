// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app's password reset mail: the app publishes a reset page and mails a link to it,
// and the test reads the mail over IMAP, pulls out the link and fetches it from the same server.
// Run: dotnet run reset-link.cs [-- <case>]
//   (no argument)  the app mails a link to the page it published
//   wrong-token    the app's link names a token it never published
//   no-link        the app's mail has no link in it
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "right";

// The server: SMTP, IMAP and HTTP on free ports, mail for example.com, and an in-memory site.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "http://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
string smtp = $"{server.BoundUrls[0]}", imap = $"{server.BoundUrls[1]}", http = $"{server.BoundUrls[2]}";
await using CurlClient client = CurlClient.Create();

// The app: a fresh token, the reset page under it, and a mail with the link.
string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
await server.Files.PutAsync($"/reset/{token}", Encoding.UTF8.GetBytes("Choose a new password for ann@example.com.\n"));
string link = mode switch { "wrong-token" => $"{http}reset/0000000000000000", "no-link" => "", _ => $"{http}reset/{token}" };
string mail = $"From: app@example.com\r\nTo: ann@example.com\r\nSubject: Reset your password\r\n\r\nOpen this link to choose a new password:\r\n{link}\r\n";
CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, smtp + "example.com")
{
    Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
    Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["ann@example.com"] },
    Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(mail))),
}, CancellationToken.None);
await sent.Body.DisposeAsync();
Console.WriteLine($"app sent: SMTP {sent.StatusCode}");

// The test: Ann's first message, its body only, over IMAP.
string body = await ReadAsync(new CurlRequest(HttpMethod.Get, $"{imap}INBOX;UID=1;SECTION=TEXT")
{
    Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" },
});

// The link: the first http address with /reset/ in it.
Match found = Regex.Match(body, @"http://\S+/reset/\S+");
if (!found.Success)
{
    Console.WriteLine("FAIL: no reset link in the mail");
    return 1;
}

Console.WriteLine($"link: {found.Value}");

// Following it, with no login, as the person resetting their password would.
CurlResponse page = await client.SendAsync(new CurlRequest(HttpMethod.Get, found.Value), CancellationToken.None);
string text = await new StreamReader(page.Body).ReadToEndAsync();
await page.Body.DisposeAsync();
if (page.StatusCode != 200)
{
    Console.WriteLine($"FAIL: the link answered {page.StatusCode} {page.ReasonPhrase}");
    return 1;
}

Console.Write($"{page.StatusCode} {page.ReasonPhrase}: {text}");
return 0;

async Task<string> ReadAsync(CurlRequest request)
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream stream = response.Body;
    return await new StreamReader(stream).ReadToEndAsync();
}
