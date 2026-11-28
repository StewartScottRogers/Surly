// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app that mails a report as an attachment: the app builds the message with Curl.Api's
// CurlMultipartContent and sends it to an in-process Surl server; the test waits for the delivery,
// reads the stored bytes with Mail.ReadMessageAsync, and checks the attachment is the report.
// Run: dotnet run send-report.cs [-- <case>]
//   (no argument)  send, then decode the attachment and compare it with the report
//   top            the same send, then the message's own header
//   parts          the same send, then each part's header
//   text           the same send, then part 1's text
//   as-is          the report attached with no content type or encoder, then part 2's header
//   missing        the report attached from a file that is not there
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "check";
byte[] report = Encoding.ASCII.GetBytes("date,builds,failures\n2026-11-17,44,2\n2026-11-18,41,0\n2026-11-19,42,3\n");

// SMTP on a free port for example.com, recording exchanges so the test can wait on events.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, RecordExchanges = true };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
await using SurlServer server = await SurlServer.StartAsync(options);

// The app: a text part and the report, base64 encoded, as one multipart message.
CurlMimePart attachment = mode switch
{
    "as-is" => CurlMimePart.Bytes(null, report) with { FileName = "report.csv" },
    "missing" => CurlMimePart.File(null, "nosuch.csv") with { ContentType = "text/csv", Encoder = "base64" },
    _ => CurlMimePart.Bytes(null, report) with { FileName = "report.csv", ContentType = "text/csv", Encoder = "base64" },
};
await using CurlClient client = CurlClient.Create();
try
{
    CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
        Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["ann@example.com"] },
        Headers = [new("Subject", "Nightly build report"), new("From", "app@example.com"), new("To", "ann@example.com")],
        Body = new CurlMultipartContent([CurlMimePart.Text(null, "The nightly build report is attached."), attachment]),
    }, CancellationToken.None);
    await sent.Body.DisposeAsync();
    Console.WriteLine($"SMTP {sent.StatusCode}");
}
catch (CurlException failed)
{
    Console.WriteLine($"not sent: curl error {(int)failed.Code} {failed.Code}");
    return 0;
}

// Delivery is asynchronous: wait for Ann's, with a bound, then read the bytes the server stored.
StoreChangedEvent delivered = (StoreChangedEvent)await server.WaitForEventAsync(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "ann@example.com" }, TimeSpan.FromSeconds(10), CancellationToken.None);
Console.WriteLine($"delivered: {delivered.Name}");
SurlMailMessage message = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", 1);
string text = Encoding.ASCII.GetString(message.Bytes.Span);

// The MIME parts: the message's header names the boundary; each part is a header, a blank line and a body.
int end = text.IndexOf("\r\n\r\n");
string header = text[..end];
string boundary = Regex.Match(header, "boundary=\"?([^\"\r\n;]+)").Groups[1].Value;
List<(string Header, string Body)> parts = text[(end + 4)..].Split("--" + boundary)
    .Skip(1).TakeWhile(p => !p.StartsWith("--"))
    .Select(p => p[2..^2])
    .Select(p => p.StartsWith("\r\n") ? ("", p[2..]) : (p[..p.IndexOf("\r\n\r\n")], p[(p.IndexOf("\r\n\r\n") + 4)..]))
    .ToList();

switch (mode)
{
    case "top":
        header.Split("\r\n").Where(l => Regex.IsMatch(l, "^(Content-Type|Mime-Version|Subject|From|To):")).ToList().ForEach(Console.WriteLine);
        break;
    case "parts" or "as-is":
        for (int i = 0; i < parts.Count; i++)
        {
            Console.WriteLine($"part {i + 1}: {(parts[i].Header == "" ? "no header" : parts[i].Header.Replace("\r\n", " | "))}");
        }

        break;
    case "text":
        Console.WriteLine(parts[0].Body);
        break;
    default:
        // The test: the part marked as an attachment, decoded from base64, is the report byte for byte.
        (string Header, string Body) part = parts.Single(p => p.Header.Contains("Content-Disposition: attachment"));
        byte[] decoded = Convert.FromBase64String(part.Body);
        string name = Regex.Match(part.Header, "filename=\"([^\"]+)\"").Groups[1].Value;
        Console.WriteLine(decoded.AsSpan().SequenceEqual(report) ? $"{name}: {decoded.Length} bytes, the same as the report" : $"{name} differs from the report");
        break;
}

return 0;
