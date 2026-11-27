// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An app places an order and returns at once; in the background it uploads a receipt over HTTP and
// mails it over SMTP. The test awaits both events on an in-process Surl.Api server, with no sleep,
// then checks what the server recorded.
// Run: dotnet run background-work.cs [-- no-mail]
//   (no argument)  the app uploads and mails, 1 s after the order
//   no-mail        the app has a bug: it uploads but never mails
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Diagnostics;
using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "right";

// HTTP for receipts and SMTP for mail, on free ports; the event record on.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "smtp://127.0.0.1:0" },
    AllowUploads = true,
    RecordExchanges = true,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass;protocols=smtp,http");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);

// One untimed request first: the first Curl.Api request in a process is slow to start.
await using (CurlClient warm = CurlClient.Create())
{
    CurlResponse warmed = await warm.SendAsync(new CurlRequest(HttpMethod.Head, $"{server.BoundUrls[0]}"), CancellationToken.None);
    await warmed.Body.DisposeAsync();
}

// The test: place the order, then wait for the receipt file and the receipt mail together.
await using var app = new OrderApp(server.BoundUrls[0], server.BoundUrls[1], mails: mode != "no-mail");
var clock = Stopwatch.StartNew();
app.PlaceOrder(7, "ann@example.com");
Console.WriteLine($"PlaceOrder returned at once: {clock.ElapsedMilliseconds < 500}");

// A wait returns as soon as its event comes; the timeout only bounds a failure.
TimeSpan timeout = TimeSpan.FromSeconds(mode == "no-mail" ? 3 : 30);
Task<SurlEvent> uploaded = server.WaitForEventAsync(new SurlEventQuery { Store = StoreKind.Content, Name = "/receipt-7.txt" }, timeout);
Task<SurlEvent> mailed = server.WaitForEventAsync(new SurlEventQuery { Store = StoreKind.Mail, Name = "ann@example.com" }, timeout);
try
{
    await Task.WhenAll(uploaded, mailed);
}
catch (TimeoutException)
{
    // WhenAll throws the first failure; each task says how it ended.
}

foreach ((string what, Task<SurlEvent> wait) in new[] { ("upload", uploaded), ("mail", mailed) })
{
    Console.WriteLine(wait.IsCompletedSuccessfully
        ? $"{what} event: {((StoreChangedEvent)wait.Result).Change} {((StoreChangedEvent)wait.Result).Name}"
        : $"{what} event: {wait.Exception!.InnerException!.Message}");
}

if (!mailed.IsCompletedSuccessfully || !uploaded.IsCompletedSuccessfully)
{
    return 1;
}

long ms = clock.ElapsedMilliseconds;
Console.WriteLine($"both came after the app's 1 s delay and well inside the timeout: {ms >= 1000 && ms < 30000}");

// What the server recorded, read without a client.
SurlFileContents receipt = await server.Files.ReadAsync("/receipt-7.txt");
Console.Write($"receipt file: {Encoding.UTF8.GetString(receipt.Bytes.Span)}");
SurlMessagePage inbox = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX");
SurlMailMessage mail = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", inbox.Messages[0].Uid);
Console.WriteLine($"mail: {Encoding.UTF8.GetString(mail.Bytes.Span).Split("\r\n").First(l => l.StartsWith("Subject: "))}");
return 0;

// The app under test. PlaceOrder starts the work and returns; the work runs on its own.
sealed class OrderApp(Uri http, Uri smtp, bool mails) : IAsyncDisposable
{
    private readonly CurlClient client = CurlClient.Create();
    private Task work = Task.CompletedTask;

    public void PlaceOrder(int order, string customer) => work = Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        string receipt = $"Receipt for order {order}: 3 widgets, 42.00\n";
        CurlResponse put = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{http}receipt-{order}.txt")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass", HttpAuthentication = HttpAuthenticationMethods.Digest },
            Body = Encoding.UTF8.GetBytes(receipt),
        }, CancellationToken.None);
        await put.Body.DisposeAsync();
        if (!mails)
        {
            return;
        }

        CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{smtp}example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [customer] },
            Body = CurlContent.FromStream(new MemoryStream(Encoding.UTF8.GetBytes($"From: app@example.com\r\nTo: {customer}\r\nSubject: Your receipt for order {order}\r\n\r\n{receipt.Replace("\n", "\r\n")}"))),
        }, CancellationToken.None);
        await sent.Body.DisposeAsync();
    });

    public async ValueTask DisposeAsync()
    {
        await work;
        await client.DisposeAsync();
    }
}
