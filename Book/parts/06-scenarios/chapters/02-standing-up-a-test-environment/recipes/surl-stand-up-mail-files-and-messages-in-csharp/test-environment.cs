// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An integration test's environment in one in-process Surl server: SMTP and IMAP for mail, HTTP for
// files, MQTT for messages. The app sends to all three with Curl.Api; the test reads each store back.
// Run: dotnet run test-environment.cs [-- <case>]
//   (no argument)  the app mails, uploads and publishes as it should; the test passes
//   wrong-topic    the app publishes on invoices/7 instead of orders/7
//   strict         the server keeps its default and refuses passwords sent in clear
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "right";

// One server, four listeners on free ports, and one account the app uses on SMTP, HTTP and MQTT.
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "http://127.0.0.1:0", "mqtt://127.0.0.1:0" },
    AllowPlaintextAuthentication = mode != "strict",
    AllowUploads = true,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass;protocols=smtp,http,mqtt");
options.Mailboxes.Add("ann@example.com:annpass");
options.Topics.Add("orders/#;retain");
await using var server = await SurlServer.StartAsync(options);
string smtp = $"{server.BoundUrls[0]}", http = $"{server.BoundUrls[2]}", mqtt = $"{server.BoundUrls[3]}";

// The app: when order 7 ships it mails the customer, uploads the invoice and publishes an event.
var login = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" };
string? published = null;
await using (CurlClient app = CurlClient.Create())
{
    try
    {
        int mailed = await Send(app, new CurlRequest(HttpMethod.Put, $"{smtp}example.com")
        {
            Authentication = login,
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["ann@example.com"] },
            Body = CurlContent.FromStream(new MemoryStream("From: app@example.com\r\nTo: ann@example.com\r\nSubject: Order 7 shipped\r\n\r\nOrder 7 is on its way.\r\n"u8.ToArray())),
        });
        Console.WriteLine($"app mailed ann: {mailed}");
        int uploaded = await Send(app, new CurlRequest(HttpMethod.Put, $"{http}invoice-7.txt")
        {
            Authentication = login with { HttpAuthentication = HttpAuthenticationMethods.Digest },
            Body = "Invoice 7: 3 widgets, 42.00\n"u8.ToArray(),
        });
        Console.WriteLine($"app uploaded the invoice: {uploaded}");
        string topic = mode == "wrong-topic" ? "invoices/7" : "orders/7";
        await Send(app, new CurlRequest(HttpMethod.Post, $"{mqtt}{topic}") { Authentication = login, Body = "shipped"u8.ToArray() });
        Console.WriteLine($"app published on {topic}");
        published = topic;
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"app failed: curl error {(int)failed.Code}: {failed.Message}");
        return 1;
    }
}

// The test: what landed in each store, read from the server without a client.
SurlMessagePage inbox = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX", 0, 10, CancellationToken.None);
SurlMailMessage mail = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", inbox.Messages[0].Uid, CancellationToken.None);
string subject = Encoding.UTF8.GetString(mail.Bytes.Span).Split("\r\n").First(l => l.StartsWith("Subject: "))["Subject: ".Length..];
SurlFileContents invoice = await server.Files.ReadAsync("/invoice-7.txt", CancellationToken.None);
SurlRetainedMessage? kept = await server.Messages.ReadRetainedAsync("orders/7", CancellationToken.None);

// The publish is MQTT QoS 0, never acknowledged, so the broker may keep it a moment after the app is
// done: the test reads again until it is there, for at most 10 s.
DateTime deadline = DateTime.UtcNow.AddSeconds(10);
while (published == "orders/7" && kept is null && DateTime.UtcNow < deadline)
{
    await Task.Delay(100);
    kept = await server.Messages.ReadRetainedAsync("orders/7", CancellationToken.None);
}

var checks = new (string Store, string Got, string Want)[]
{
    ("mail", subject, "Order 7 shipped"),
    ("file", Encoding.UTF8.GetString(invoice.Bytes.Span).TrimEnd(), "Invoice 7: 3 widgets, 42.00"),
    ("message", kept is null ? "(none)" : Encoding.UTF8.GetString(kept.Payload.Span), "shipped"),
};
foreach ((string store, string got, string want) in checks)
{
    Console.WriteLine(got == want ? $"{store}: {got}" : $"FAIL: {store} is {got}, want {want}");
}

return checks.All(c => c.Got == c.Want) ? 0 : 1;

// One request; the status code, with the body read and discarded.
static async Task<int> Send(CurlClient client, CurlRequest request)
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode;
}
