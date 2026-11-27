// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app's report mail: the app attaches report.csv with SmtpClient and sends it to an
// in-process Surl server; the test fetches the attachment's part over IMAP with Curl.Api, decodes
// it and compares its bytes with the report.
// Run: dotnet run check-attachment.cs [-- <case>]
//   (no argument)  the app attaches today's report; the test passes
//   old-report     the app attaches last week's report
//   no-attachment  the app forgets the attachment
//   no-plaintext   the server keeps its default, no password in clear without TLS
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Net;
using System.Net.Mail;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "right";
byte[] report = "date,builds,failures\n2026-11-17,44,2\n2026-11-18,41,0\n2026-11-19,42,3\n2026-11-20,40,1\n"u8.ToArray();
byte[] oldReport = "date,builds,failures\n2026-11-17,44,2\n2026-11-18,41,0\n2026-11-19,42,3\n"u8.ToArray();

// The server: SMTP and IMAP on free ports over one mail store, serving example.com.
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    AllowPlaintextAuthentication = mode != "no-plaintext",
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
Uri smtp = server.BoundUrls[0];
string imap = $"{server.BoundUrls[1]}";

// The app under test sends the report as an attachment, its mistakes chosen by the argument.
using (var app = new SmtpClient(smtp.Host, smtp.Port) { Credentials = new NetworkCredential("app@example.com", "apppass") })
using (var mail = new MailMessage("app@example.com", "ann@example.com", "Nightly build report", "The nightly build report is attached."))
{
    if (mode != "no-attachment")
    {
        mail.Attachments.Add(new Attachment(new MemoryStream(mode == "old-report" ? oldReport : report), "report.csv", "text/csv"));
    }

    try
    {
        app.Send(mail);
    }
    catch (SmtpException refused)
    {
        Console.WriteLine($"app could not send: {refused.StatusCode}: {refused.Message}");
        return 1;
    }
}

// The test: part 2 of Ann's first message, its header and then its body, over IMAP.
await using CurlClient client = CurlClient.Create();
string header = await FetchAsync("INBOX;UID=1;SECTION=2.MIME");
string body = await FetchAsync("INBOX;UID=1;SECTION=2");
string? type = header.Split("\r\n").FirstOrDefault(line => line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase));
Console.WriteLine(type ?? "no part 2");
if (body.Length == 0)
{
    Console.WriteLine("FAIL: no attachment");
    return 1;
}

// The body is base64; FromBase64String skips the line ends. Then compare byte for byte.
byte[] attached = Convert.FromBase64String(body);
bool same = attached.AsSpan().SequenceEqual(report);
Console.WriteLine(same ? $"attachment matches report.csv, {attached.Length} bytes" : $"FAIL: attachment is {attached.Length} bytes and differs from report.csv, {report.Length} bytes");
return same ? 0 : 1;

async Task<string> FetchAsync(string path)
{
    CurlRequest fetch = new(HttpMethod.Get, imap + path)
    {
        Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" },
    };
    CurlResponse response = await client.SendAsync(fetch, CancellationToken.None);
    await using Stream stream = response.Body;
    return await new StreamReader(stream).ReadToEndAsync();
}
