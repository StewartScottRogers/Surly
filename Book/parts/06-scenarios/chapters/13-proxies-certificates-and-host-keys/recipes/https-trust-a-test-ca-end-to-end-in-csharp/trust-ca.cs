// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A C# test whose HTTPS and SMTPS servers carry certificates one test certificate authority signed,
// and whose clients trust that authority alone. The certificates come from setup: in recipe.yml.
// Run: dotnet run trust-ca.cs [-- <case>]
//   (no argument)  trust ca.pem: fetch over HTTPS, send over SMTPS, find the mail in Ann's mailbox
//   no-ca          trust nothing: both clients fail
//   stray          trust ca.pem, against a server another authority signed for
//   insecure       skip the check against that server
//   revocation     trust ca.pem, without NoRevoke
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Tls;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "trusted";

// HTTPS with the www certificate; or, for stray and insecure, a certificate another authority signed.
var webOptions = mode is "stray" or "insecure"
    ? new SurlServerOptions { ListenUrls = { "https://127.0.0.1:0" }, CertificateFile = "stray.pem", KeyFile = "stray.key" }
    : new SurlServerOptions { ListenUrls = { "https://127.0.0.1:0" }, CertificateFile = "www.pem", KeyFile = "www.key" };
webOptions.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer web = await SurlServer.StartAsync(webOptions);

// SMTPS with the mail certificate, for example.com: the app's mailbox and Ann's.
var mailOptions = new SurlServerOptions { ListenUrls = { "smtps://127.0.0.1:0" }, CertificateFile = "mail.pem", KeyFile = "mail.key", RecordExchanges = true };
mailOptions.MailDomains.Add("example.com");
mailOptions.Mailboxes.Add("app@example.com:apppass");
mailOptions.Mailboxes.Add("ann@example.com:annpass");
await using SurlServer mail = await SurlServer.StartAsync(mailOptions);

// The clients' trust: the test CA alone. NoRevoke is --ssl-no-revoke, for Windows, which asks
// about revocation and finds no revocation list in a test certificate.
var peer = new CurlTlsPeerOptions
{
    CertificateAuthorityFile = mode == "no-ca" ? null : "ca.pem",
    SslOptions = mode == "revocation" ? SslOptions.None : SslOptions.NoRevoke,
    VerifyPeer = mode != "insecure",
    VerifyHost = mode != "insecure",
};
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = peer } });

try
{
    CurlResponse page = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{web.BoundUrl}hello.txt"), CancellationToken.None);
    await using (Stream body = page.Body)
    {
        Console.WriteLine($"HTTPS {(int)page.StatusCode}: {(await new StreamReader(body).ReadToEndAsync()).TrimEnd()}");
    }
}
catch (CurlException e)
{
    Console.WriteLine($"HTTPS refused: {e.Code} ({(int)e.Code})");
}

if (mode is "stray" or "insecure" or "revocation")
{
    return 0;
}

try
{
    CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{mail.BoundUrl}client.example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
        Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["ann@example.com"] },
        Body = "From: app@example.com\r\nTo: ann@example.com\r\nSubject: Signed by the test CA\r\n\r\nThis came over SMTPS.\r\n"u8.ToArray(),
    }, CancellationToken.None);
    await sent.Body.DisposeAsync();
    Console.WriteLine($"SMTPS {sent.StatusCode}");
}
catch (CurlException e)
{
    Console.WriteLine($"SMTPS refused: {e.Code} ({(int)e.Code})");
    return 0;
}

// Delivery is asynchronous: wait, with a bound, for the mail to reach Ann's mailbox.
await mail.WaitForEventAsync(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "ann@example.com" }, TimeSpan.FromSeconds(10), CancellationToken.None);
Console.WriteLine($"ann@example.com holds {(await mail.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages.Count}");
return 0;
