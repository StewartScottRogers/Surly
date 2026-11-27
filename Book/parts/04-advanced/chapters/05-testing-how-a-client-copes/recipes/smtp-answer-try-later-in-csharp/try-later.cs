// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of what a mailer does when the server answers a recipient command 451, try later: an
// in-process Surl server answers the first RCPT, or the first two, with that temporary error, the
// app sends with Curl.Api, and the test reads what the store holds.
// Run: dotnet run try-later.cs [-- <case>]
//   (no argument)  the first RCPT is answered 451; the same send again is accepted
//   received       the same, and the EHLO name in the stored message's Received line
//   log            the same, and the server's log lines for the refusal and the stored message
//   abandon        two recipients, the first RCPT refused (one of two answers): the message is abandoned
//   allowfails     the same send with MailRecipientAllowFails (the second answer): bob alone gets it
//   spent          then a plain send to ann, the rule spent
//   bad-reply      a rule whose reply is a code with no text
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Collections.Concurrent;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "retry";
bool twice = mode is "abandon" or "allowfails" or "spent";

// SMTP on a free port for example.com, the app's mailbox and one each for ann and bob, and one fault rule on RCPT.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, Log = log, LogLevel = LogLevel.Verbose };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("bob@example.com:bobpass");
options.Faults.Add(SurlFault.ProtocolError(mode == "bad-reply" ? "451" : "451 4.3.0 try later")
    with { Scheme = "smtp", Command = "RCPT", Times = twice ? 2 : 1 });
try
{
    await using SurlServer server = await SurlServer.StartAsync(options);
    return await Run(server);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"start refused: {refused.ExitCode}: {refused.Lines[0]}");
    return 0;
}

async Task<int> Run(SurlServer server)
{
    // Delivery is asynchronous: every delivery is an event, and a wait for the next one has a bound.
    var deliveries = new ConcurrentQueue<string>();
    SemaphoreSlim arrived = new(0);
    using IDisposable watching = server.OnEvent(
        new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
        (e, cancellationToken) =>
        {
            deliveries.Enqueue(((StoreChangedEvent)e).Name);
            arrived.Release();
            return ValueTask.CompletedTask;
        });

    // The app: one message, logged in as app@example.com; the EHLO name is the URL's path.
    await using CurlClient client = CurlClient.Create();
    async Task<bool> Send(string ehlo, string[] to, bool allowFails = false)
    {
        var send = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}{ehlo}")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = to, MailRecipientAllowFails = allowFails },
            Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(
                "From: app@example.com\r\nTo: ann@example.com\r\nSubject: Order 1042 has shipped\r\n\r\nYour order is on its way.\r\n"))),
        };
        try
        {
            CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
            await sent.Body.DisposeAsync();
            Console.WriteLine($"{ehlo}: SMTP {sent.StatusCode}");
            return await arrived.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (CurlException refused)
        {
            Console.WriteLine($"{ehlo}: refused, curl error {(int)refused.Code} {refused.Code}: {refused.Message}");
            return true;
        }
    }

    async Task<int> Count(string owner) => (await server.Mail.ListMessagesAsync(owner, "INBOX")).Messages.Count;

    string[] ann = ["ann@example.com"];
    string[] both = ["ann@example.com", "bob@example.com"];
    bool delivered = true;
    switch (mode)
    {
        case "abandon":
            await Send("first", both);
            break;
        case "allowfails":
            await Send("first", both);
            delivered = await Send("second", both, allowFails: true);
            break;
        case "spent":
            await Send("first", both);
            await Send("second", both, allowFails: true);
            delivered = await Send("third", ann);
            break;
        default:
            await Send("first", ann);
            delivered = await Send("second", ann);
            break;
    }

    if (!delivered)
    {
        Console.WriteLine("no delivery within 10 s");
        return 1;
    }

    // The test: the rule and the store, read with no client.
    SurlFaultRule rule = server.Faults.List()[0];
    Console.WriteLine($"rule {rule.Id} remaining: {rule.Remaining}");
    foreach (string owner in new[] { "ann@example.com", "bob@example.com" })
    {
        if (twice || owner == "ann@example.com")
        {
            Console.WriteLine($"{owner} holds {await Count(owner)}");
        }
    }

    if (mode == "received")
    {
        SurlMailMessage copy = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", 1);
        string received = Encoding.ASCII.GetString(copy.Bytes.Span).Split("\r\n").First(l => l.StartsWith("Received: "));
        Console.WriteLine(received[..received.IndexOf(" (")]);
    }

    if (mode == "log")
    {
        // The log line can trail the delivery by a moment: look again until there is one per delivery, for up to 10 s.
        for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
        {
            if (System.Text.RegularExpressions.Regex.Matches(log.ToString(), "Message stored: ").Count >= deliveries.Count) break;
        }

        string text = log.ToString();
        Console.WriteLine($"refusals logged: {System.Text.RegularExpressions.Regex.Matches(text, "RCPT answered with the chosen error of fault rule f1; nothing was stored.").Count}");
        Console.WriteLine($"stored logged: {System.Text.RegularExpressions.Regex.Matches(text, "Message stored: ").Count}");
    }

    return 0;
}
