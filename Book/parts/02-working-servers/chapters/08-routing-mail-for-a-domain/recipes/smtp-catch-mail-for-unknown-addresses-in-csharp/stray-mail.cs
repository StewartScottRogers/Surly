// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An app that may mail addresses no test set up: a Surl catch-all keeps that mail, and the test
// waits for a delivery event, then reads what was caught from the store.
// Run: dotnet run stray-mail.cs [-- <case>]
//   (no argument)  two stray addresses and ann, on a server with a catch-all
//   no-catch-all   the same mail, on a server without one
//   letter-case    Ann@Example.COM
//   alias          support@example.com, an alias for ann
//   other-domain   one message to stray@ and team@example.org, a domain with no catch-all
//   envelope-only  a message to jo@example.com whose To header names ann
//   outside        one address outside every mail domain
//   twice          two catch-alls for one domain
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Collections.Concurrent;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "catch-all";

// SMTP on a free port; example.com with the app's mailbox, ann's and the catch-all's; example.org with team's.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.MailDomains.Add("example.org");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("catchall@example.com:catchpass");
options.Mailboxes.Add("team@example.org:teampass");
options.MailAliases.Add("support@example.com=ann@example.com");
if (mode != "no-catch-all")
{
    options.MailCatchAlls.Add("example.com=catchall@example.com");
}

if (mode == "twice")
{
    options.MailCatchAlls.Add("example.com=ann@example.com");
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"refused at start: {refused.ExitCode}, {refused.Lines[0]}");
    return 1;
}

await using (server)
{
    string smtp = $"{server.BoundUrls[0]}";

    // What the app sends in each case: the envelope recipients, the To header, and whose delivery comes last.
    (string[] To, string Header)[] sends = mode switch
    {
        "letter-case" => [(["Ann@Example.COM"], "Ann@Example.COM")],
        "alias" => [(["support@example.com"], "support@example.com")],
        "other-domain" => [(["stray@example.org", "team@example.org"], "stray@example.org, team@example.org")],
        "envelope-only" => [(["jo@example.com"], "ann@example.com")],
        "outside" => [(["someone@example.net"], "someone@example.net")],
        _ => [(["anne@example.com"], "anne@example.com"), (["bob.smith@example.com"], "bob.smith@example.com"), (["ann@example.com"], "ann@example.com")],
    };
    string last = mode switch { "other-domain" => "team@example.org", "envelope-only" => "catchall@example.com", _ => "ann@example.com" };

    // Delivery is asynchronous: every delivery is an event, and the program waits on them, with a bound.
    var deliveries = new ConcurrentQueue<string>();
    var lastDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using IDisposable watching = server.OnEvent(
        new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
        (e, cancellationToken) =>
        {
            string name = ((StoreChangedEvent)e).Name;
            deliveries.Enqueue(name);
            if (name.StartsWith(last + "/"))
            {
                lastDelivered.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

    // The app: one message per send, logged in as app@example.com.
    await using CurlClient client = CurlClient.Create();
    foreach ((string[] to, string header) in sends)
    {
        var send = new CurlRequest(HttpMethod.Put, smtp + "example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = to },
            Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes($"From: app@example.com\r\nTo: {header}\r\nSubject: Welcome\r\n\r\nYour account is ready.\r\n"))),
        };
        try
        {
            CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
            await sent.Body.DisposeAsync();
            Console.WriteLine($"sent to {string.Join(", ", to)}: {sent.StatusCode}");
        }
        catch (CurlException refused)
        {
            Console.WriteLine($"sent to {string.Join(", ", to)}: curl error {(int)refused.Code}, {refused.Message}");
            return 1;
        }
    }

    // Deliveries happen in order, so once the last one has come, those before it have been dealt with.
    if (await Task.WhenAny(lastDelivered.Task, Task.Delay(TimeSpan.FromSeconds(10))) != lastDelivered.Task)
    {
        Console.WriteLine($"no delivery to {last} within 10 s");
        return 1;
    }

    Console.WriteLine($"delivered: {string.Join(", ", deliveries)}");

    // The test: what the catch-all holds, and to whom each message says it was written.
    SurlMessagePage caught = await server.Mail.ListMessagesAsync("catchall@example.com", "INBOX");
    foreach (SurlMessageSummary message in caught.Messages)
    {
        SurlMailMessage read = await server.Mail.ReadMessageAsync("catchall@example.com", "INBOX", message.Uid);
        string to = Encoding.ASCII.GetString(read.Bytes.Span).Split("\r\n").First(h => h.StartsWith("To: "));
        Console.WriteLine($"caught UID {message.Uid}: {to}");
    }

    Console.WriteLine($"stray messages caught: {caught.Messages.Count}");
    return caught.Messages.Count == 2 ? 0 : 1;
}
