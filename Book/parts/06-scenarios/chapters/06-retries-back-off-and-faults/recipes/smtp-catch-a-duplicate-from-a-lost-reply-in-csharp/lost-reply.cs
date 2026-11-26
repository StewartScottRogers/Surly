// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a mail sender that tries once more when a send fails: the in-process server cuts the
// connection one byte into the reply that confirms the message, and the test counts the copies of
// the order's Message-ID in Ann's mailbox. One copy passes; any other count fails, exit 1.
// Run: dotnet run lost-reply.cs [-- <case>]
//   (none)       the sender with one retry (the default)
//   tries        the same, then what the server sent from its 354 on in each try, and its deliveries
//   no-retry     the sender without a retry
//   cut-before   the cut one byte sooner, at the end of the 354
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "retry";
const string MessageId = "<order-1042@shop.example.com>";

// SMTP on a free port for example.com, recording every exchange. The rule cuts the next SMTP
// connection cleanly once the server has sent 320 bytes: the 319 up to the end of its 354, and one more.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, RecordExchanges = true };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("shop@example.com:shoppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Faults.Add(SurlFault.DropConnection() with
{
    Scheme = "smtp", AfterBytes = mode == "cut-before" ? 319 : 320, Close = FaultClose.Graceful, Times = 1,
});
await using SurlServer server = await SurlServer.StartAsync(options);

// The sender under test: one send, and one more if it fails.
int allowed = mode == "no-retry" ? 1 : 2;
await using (CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(10) }))
{
    for (int attempt = 1; attempt <= allowed; attempt++)
    {
        try
        {
            CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}shop.example.com")
            {
                Authentication = new CurlAuthenticationOptions { UserName = "shop@example.com", Password = "shoppass" },
                Mail = new CurlMailOptions { MailFrom = "shop@example.com", MailRecipients = ["ann@example.com"] },
                Body = Encoding.ASCII.GetBytes(
                    $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order 1042 confirmed\r\nMessage-ID: {MessageId}\r\n\r\nThank you for order 1042.\r\n"),
            }, CancellationToken.None);
            await sent.Body.DisposeAsync();
            Console.WriteLine($"try {attempt}: SMTP {sent.StatusCode}");
            break;
        }
        catch (CurlException e)
        {
            Console.WriteLine($"try {attempt}: CurlException {e.Code}");
        }
    }
}

// The test: how many messages in Ann's INBOX carry the order's Message-ID.
int copies = 0;
foreach (SurlMessageSummary info in (await server.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages)
{
    SurlMailMessage message = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", info.Uid);
    copies += Encoding.ASCII.GetString(message.Bytes.Span).Split("\r\n").Count(l => l == $"Message-ID: {MessageId}");
}

Console.WriteLine($"{MessageId} stored {copies} time(s)");
Console.WriteLine(copies == 1 ? "PASS" : "FAIL: the order was not stored exactly once");

if (mode == "tries")
{
    // The client is disposed and has sent QUIT; wait, with a bound, until both exchanges have ended.
    for (int waited = 0; server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "smtp" }).Count < 2 && waited < 100; waited++)
    {
        await Task.Delay(100);
    }

    foreach (SurlExchangeRecord exchange in server.Exchanges.Where(e => e.Scheme == "smtp"))
    {
        // What the server sent, a reply line at a time, with the bytes it had sent by the end of each.
        long total = 0;
        var after354 = new List<string>();
        long at354 = -1;
        foreach (SurlExchangeChunk chunk in exchange.Chunks.Where(c => c.Direction == SurlExchangeDirection.Sent))
        {
            total += chunk.Bytes.Length;
            string text = Encoding.ASCII.GetString(chunk.Bytes.Span).TrimEnd('\r', '\n');
            if (at354 >= 0)
            {
                after354.Add(text);
            }
            else if (text.StartsWith("354 "))
            {
                at354 = total;
            }
        }

        Console.WriteLine($"exchange {exchange.ExchangeId}: {at354} bytes to the end of the 354, then {string.Join(" | ", after354)}");
    }

    var deliveries = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Scheme = "smtp" })
        .Cast<StoreChangedEvent>().Select(e => $"{e.ExchangeId}");
    Console.WriteLine($"deliveries from exchanges {string.Join(", ", deliveries)}");
}

return copies == 1 ? 0 : 1;
