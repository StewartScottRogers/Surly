// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that reacts the moment mail is delivered: OnEvent registers a callback on the in-process Surl
// server's mail store, the app sends over SMTP with Curl.Api, and the callback runs for each delivery
// with no polling. Each case shows one thing a callback can do.
// Run: dotnet run on-delivery.cs [-- <case>]
//   (no argument)  one mail to ann: the callback reads the event and the message
//   filter         a query with Name set: mail to bob and then to ann, and the callback sees ann's only
//   dispose        the callback is disposed, and a later delivery does not reach it
//   two            two callbacks on the same query, each called for every delivery
//   order          three mails to ann: the callback runs in delivery order
//   kinds          the other changes to the store a callback is told about: Seeded and Reset
//   react          the callback itself delivers a reply into the sender's mailbox
//   throws         a callback that throws: the delivery and the other callbacks are not affected
//   unknown        mail to an address with no mailbox: no delivery, no event
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Collections.Concurrent;
using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "one";

// SMTP for example.com: the app's mailbox, ann's, bob's and the support desk's.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
foreach (string name in new[] { "app", "ann", "bob", "support" })
{
    options.Mailboxes.Add($"{name}@example.com:{name}pass");
}

await using SurlServer server = await SurlServer.StartAsync(options);

// The callback: it runs for every event the query matches, off the serving path, one at a time, in order.
var seen = new ConcurrentQueue<string>();
var calls = new SemaphoreSlim(0);
var query = new SurlEventQuery
{
    Kind = SurlEventKind.StoreChanged,
    Store = StoreKind.Mail,
    Name = mode == "filter" ? "ann@example.com" : mode == "react" ? "support@example.com" : null,
};
Func<string, Func<SurlEvent, CancellationToken, ValueTask>> callback = label => async (e, cancellationToken) =>
{
    var change = (StoreChangedEvent)e;
    string line = $"{label}{change.Change} {change.Name}";
    if (change.Change == StoreChange.Delivered)
    {
        string name = change.Name;
        string owner = name[..name.IndexOf('/')];
        uint uid = uint.Parse(name[(name.LastIndexOf('=') + 1)..]);
        SurlMailMessage message = await server.Mail.ReadMessageAsync(owner, "INBOX", uid, cancellationToken);
        string subject = Encoding.ASCII.GetString(message.Bytes.Span).Split("\r\n").First(l => l.StartsWith("Subject: "));
        line += $", {(change.ByteCount == message.Bytes.Length ? "bytes match the stored message" : "bytes differ")}, exchange {(change.ExchangeId is null ? "none" : "recorded")}, {subject}";
        if (mode == "react")
        {
            // A reaction: the callback delivers an acknowledgement into the sender's own mailbox.
            await server.Mail.DeliverAsync("app@example.com", Encoding.ASCII.GetBytes("From: support@example.com\r\nTo: app@example.com\r\nSubject: Re: " + subject[9..] + "\r\n\r\nWe have it.\r\n"), cancellationToken);
        }
    }

    seen.Enqueue(line);
    calls.Release();
};
IDisposable first = server.OnEvent(query, callback(""));
IDisposable second = mode == "two" ? server.OnEvent(query, callback("again: ")) : first;

// A witness for the dispose case, and a callback that fails for the throws case; neither is the test's own.
var witnessed = new SemaphoreSlim(0);
using IDisposable witness = server.OnEvent(query, (e, cancellationToken) =>
{
    witnessed.Release();
    return ValueTask.CompletedTask;
});
using IDisposable failing = mode == "throws"
    ? server.OnEvent(query, (e, cancellationToken) => throw new InvalidOperationException("the callback failed"))
    : first;

// The app, and how many callback runs the test waits for: each wait has a bound.
await using CurlClient client = CurlClient.Create();
async Task Send(string to, string subject)
{
    var send = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
        Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [to] },
        Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes($"From: app@example.com\r\nTo: {to}\r\nSubject: {subject}\r\n\r\nHello.\r\n"))),
    };
    CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
    await sent.Body.DisposeAsync();
    Console.WriteLine($"app sent to {to}: SMTP {sent.StatusCode}");
}

async Task<bool> Await(int runs)
{
    for (int i = 0; i < runs; i++)
    {
        if (!await calls.WaitAsync(TimeSpan.FromSeconds(10)))
        {
            return false;
        }
    }

    return true;
}

bool ok = true;
switch (mode)
{
    case "filter":
        await Send("bob@example.com", "For Bob");
        await Send("ann@example.com", "For Ann");
        ok = await Await(1);
        break;
    case "dispose":
        await Send("ann@example.com", "Before");
        ok = await Await(1);
        first.Dispose();
        await Send("ann@example.com", "After");
        // The delivery happened all the same: the witness, a callback that was not disposed, hears both.
        for (int i = 0; i < 2; i++)
        {
            ok = ok && await witnessed.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Console.WriteLine($"witness heard 2 deliveries: {ok}");
        break;
    case "two":
        await Send("ann@example.com", "Hello");
        ok = await Await(2);
        break;
    case "order":
        await Send("ann@example.com", "First");
        await Send("ann@example.com", "Second");
        await Send("ann@example.com", "Third");
        ok = await Await(3);
        break;
    case "kinds":
        await server.Mail.DeliverAsync("ann@example.com", Encoding.ASCII.GetBytes("Subject: Seeded\r\n\r\nHello.\r\n"));
        await server.ResetAsync();
        ok = await Await(2);
        break;
    case "react":
        await Send("support@example.com", "Help with my order");
        ok = await Await(1);
        break;
    case "throws":
        await Send("ann@example.com", "First");
        await Send("ann@example.com", "Second");
        ok = await Await(2);
        break;
    case "unknown":
        await Send("nobody@example.com", "To nobody");
        Console.WriteLine($"a callback within 2 s: {await calls.WaitAsync(TimeSpan.FromSeconds(2))}");
        break;
    default:
        await Send("ann@example.com", "Order 1042 has shipped");
        ok = await Await(1);
        break;
}

second.Dispose();
first.Dispose();
foreach (string line in seen)
{
    Console.WriteLine($"callback: {line}".TrimEnd());
}

if (mode == "react")
{
    Console.WriteLine($"app holds {(await server.Mail.ListMessagesAsync("app@example.com", "INBOX")).Messages.Count}");
}

if (mode == "dispose")
{
    Console.WriteLine($"callback ran {seen.Count} time(s)");
}

if (!ok)
{
    Console.WriteLine("no callback within 10 s");
}

return ok ? 0 : 1;
