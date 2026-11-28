// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that collects every mail delivered while the app runs: a callback on the in-process Surl
// server's delivery events reads each new message into a list, and the test asserts on the list.
// Run: dotnet run collect-mail.cs [-- <case>]
//   (no argument)  the app sends three order confirmations, to ann, bob and ann
//   alias          the app sends one mail to team@example.com, an alias for ann and bob
//   unknown        the app sends one mail to nobody@example.com, an address with no mailbox
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "orders";

// SMTP for example.com: the app's mailbox, two customers, and team@ as an alias for both.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("bob@example.com:bobpass");
options.MailAliases.Add("team@example.com=ann@example.com,bob@example.com");
await using var server = await SurlServer.StartAsync(options);

// What the app will send in this case, and how many deliveries the test expects.
(string To, string Subject)[] mails = mode switch
{
    "alias" => [("team@example.com", "Stock count on Friday")],
    "unknown" => [("nobody@example.com", "Order 1004 confirmed")],
    _ => [("ann@example.com", "Order 1001 confirmed"), ("bob@example.com", "Order 1002 confirmed"), ("ann@example.com", "Order 1003 confirmed")],
};
int expected = mode == "alias" ? 2 : mails.Length;

// The collector: one entry per delivery, read from the store as it happens.
var collected = new List<string>();
var allIn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
using IDisposable collecting = server.OnEvent(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    async (e, cancellationToken) =>
    {
        var change = (StoreChangedEvent)e;                 // e.g. ann@example.com/INBOX/;UID=1
        if (change.Change != StoreChange.Delivered)
        {
            return;
        }

        string owner = change.Name[..change.Name.IndexOf('/')];
        string mailbox = change.Name[(owner.Length + 1)..change.Name.IndexOf("/;UID=", StringComparison.Ordinal)];
        uint uid = uint.Parse(change.Name[(change.Name.LastIndexOf('=') + 1)..]);
        SurlMailMessage message = await server.Mail.ReadMessageAsync(owner, mailbox, uid, cancellationToken);
        string subject = Encoding.ASCII.GetString(message.Bytes.Span).Split("\r\n").First(line => line.StartsWith("Subject: "));
        lock (collected)
        {
            collected.Add($"{owner} {mailbox} UID {uid}: {subject}");
            if (collected.Count == expected)
            {
                allIn.TrySetResult();
            }
        }
    });

// The app runs: it sends each mail over SMTP with Curl.Api.
await using (CurlClient client = CurlClient.Create())
{
    foreach ((string to, string subject) in mails)
    {
        var send = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [to] },
            Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes($"From: app@example.com\r\nTo: {to}\r\nSubject: {subject}\r\n\r\nThank you.\r\n"))),
        };
        CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
        await sent.Body.DisposeAsync();
        Console.WriteLine($"app sent to {to}: SMTP {sent.StatusCode}");
    }
}

// The test: wait, at most two seconds, for the collector to hold what it expects, then assert on it.
bool complete = await Task.WhenAny(allIn.Task, Task.Delay(TimeSpan.FromSeconds(2))) == allIn.Task;
lock (collected)
{
    Console.WriteLine($"collected {collected.Count} of {expected}{(complete ? "" : " within 2 s")}:");
    collected.ForEach(entry => Console.WriteLine($"  {entry}"));
}

return complete ? 0 : 1;
