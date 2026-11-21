// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app that sends one notice to several people: an in-process Surl server takes it,
// the app sends with Curl.Api, and the test waits for the deliveries and checks each mailbox in the
// server's store with Mail.ListMessagesAsync.
// Run: dotnet run send-notice.cs [-- <case>]
//   (no argument)  one message to Ann, Bob and Cy
//   header         the To field of Bob's copy
//   log            the server's log line for the stored message
//   refused        Dee and Carol, who is outside the domain
//   refused-none   the same, and what Dee holds afterwards
//   allowfails     Fay and Carol, with MailRecipientAllowFails
//   unknown        Gus and an address in the domain that is no mailbox
//   twice          Hal twice and Ivy once
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Collections.Concurrent;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "three";

// SMTP on a free port for example.com: the app's mailbox and one for each person who might be named.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, Log = log, LogLevel = LogLevel.Verbose };
options.MailDomains.Add("example.com");
foreach (string name in new[] { "app", "ann", "bob", "cy", "dee", "fay", "gus", "hal", "ivy" })
{
    options.Mailboxes.Add($"{name}@example.com:{name}pass");
}

await using SurlServer server = await SurlServer.StartAsync(options);

// Who the app names, and which mailbox is the last one to be filled.
(string[] to, string last, bool allowFails) = mode switch
{
    "refused" or "refused-none" => (new[] { "dee@example.com", "carol@elsewhere.org" }, "", false),
    "allowfails" => (new[] { "fay@example.com", "carol@elsewhere.org" }, "fay@example.com", true),
    "unknown" => (new[] { "gus@example.com", "nobody@example.com" }, "gus@example.com", false),
    "twice" => (new[] { "hal@example.com", "hal@example.com", "ivy@example.com" }, "ivy@example.com", false),
    _ => (new[] { "ann@example.com", "bob@example.com", "cy@example.com" }, "cy@example.com", false),
};

// Delivery is asynchronous: every delivery is an event, and the program waits for the last one, with a bound.
var deliveries = new ConcurrentQueue<string>();
var lastDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
using IDisposable watching = server.OnEvent(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    (e, cancellationToken) =>
    {
        string name = ((StoreChangedEvent)e).Name;
        deliveries.Enqueue(name);
        if (last != "" && name.StartsWith(last + "/"))
        {
            lastDelivered.TrySetResult();
        }

        return ValueTask.CompletedTask;
    });

// The app: one message, one envelope with every recipient, logged in as app@example.com.
await using CurlClient client = CurlClient.Create();
var send = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}example.com")
{
    Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
    Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = to, MailRecipientAllowFails = allowFails },
    Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(
        "From: app@example.com\r\nTo: ann@example.com\r\nSubject: Maintenance on Saturday\r\n\r\nThe system is down from 02:00 to 04:00.\r\n"))),
};
try
{
    CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
    await sent.Body.DisposeAsync();
    Console.WriteLine($"SMTP {sent.StatusCode}");
}
catch (CurlException refused)
{
    Console.WriteLine($"refused: curl error {(int)refused.Code} {refused.Code}: {refused.Message}");
}

if (last != "")
{
    if (await Task.WhenAny(lastDelivered.Task, Task.Delay(TimeSpan.FromSeconds(10))) != lastDelivered.Task)
    {
        Console.WriteLine($"no delivery to {last} within 10 s");
        return 1;
    }

    Console.WriteLine($"delivered: {string.Join(", ", deliveries.Order())}");
}

// The test: what each mailbox holds, read from the store with no client.
switch (mode)
{
    case "header":
        SurlMailMessage copy = await server.Mail.ReadMessageAsync("bob@example.com", "INBOX", 1);
        Console.WriteLine(Encoding.ASCII.GetString(copy.Bytes.Span).Split("\r\n").First(l => l.StartsWith("To: ")));
        break;
    case "log":
        int stored = System.Text.RegularExpressions.Regex.Matches(log.ToString(), @"Message stored: \d+ bytes for 3 recipients").Count;
        Console.WriteLine($"stored once for 3 recipients: {stored}");
        break;
    case "refused" or "refused-none":
        if (mode == "refused-none")
        {
            Console.WriteLine($"dee@example.com holds {await Count("dee@example.com")}");
        }

        break;
    case "allowfails" or "unknown":
        Console.WriteLine($"{to[0]} holds {await Count(to[0])}");
        break;
    case "twice":
        Console.WriteLine($"hal@example.com holds {await Count("hal@example.com")}");
        Console.WriteLine($"ivy@example.com holds {await Count("ivy@example.com")}");
        break;
    default:
        foreach (string owner in to)
        {
            Console.WriteLine($"{owner} holds {await Count(owner)}");
        }

        break;
}

return 0;

async Task<int> Count(string owner) => (await server.Mail.ListMessagesAsync(owner, "INBOX")).Messages.Count;
