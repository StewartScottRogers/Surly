// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test collects every delivery the app makes over SMTP with an OnEvent callback filtered to
// SMTP's mail store changes, so a welcome message the test seeds itself stays out of the list.
// Run: dotnet run collect-deliveries.cs [-- <case>]
//   (no argument)  three orders, to Ann, Bob and Ann; the deliveries collected
//   unfiltered     the same, with a query that leaves out Scheme
//   sizes          the same, and each event's ByteCount beside the stored size
//   two            one order to Ann and Bob in one send
//   unknown        one order to an address with no mailbox
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Collections.Concurrent;
using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "orders";

// SMTP on a free port for example.com: the shop app's mailbox, Ann's and Bob's.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("shop@example.com:shoppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("bob@example.com:bobpass");
await using SurlServer server = await SurlServer.StartAsync(options);

// The collector: every mail store change that came through an smtp:// listen URL.
var query = new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Scheme = "smtp" };
if (mode == "unfiltered")
{
    query = new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail };
}

var collected = new ConcurrentQueue<StoreChangedEvent>();
using IDisposable registration = server.OnEvent(query, (e, ct) =>
{
    collected.Enqueue((StoreChangedEvent)e);
    return ValueTask.CompletedTask;
});

// The test's own seed, with the collector already listening: a welcome message in Ann's INBOX.
await server.Mail.DeliverAsync("ann@example.com",
    Encoding.ASCII.GetBytes("From: shop@example.com\r\nSubject: Welcome\r\n\r\nWelcome to the shop.\r\n"), CancellationToken.None);

// The app: one order confirmation per send, over SMTP with its own login.
string[][] sends = mode switch
{
    "two" => [["ann@example.com", "bob@example.com"]],
    "unknown" => [["cy@example.com"]],
    _ => [["ann@example.com"], ["bob@example.com"], ["ann@example.com"]],
};
await using (CurlClient client = CurlClient.Create())
{
    int order = 1041;
    foreach (string[] to in sends)
    {
        order++;
        CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}shop.example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "shop@example.com", Password = "shoppass" },
            Mail = new CurlMailOptions { MailFrom = "shop@example.com", MailRecipients = to },
            Body = Encoding.ASCII.GetBytes($"From: shop@example.com\r\nSubject: Order {order} confirmed\r\n\r\nThank you.\r\n"),
        }, CancellationToken.None);
        await sent.Body.DisposeAsync();
    }
}

// Wait, at most ten seconds, until the deliveries the test expects have been collected.
int expected = mode switch { "two" => 2, "unknown" => 1, "unfiltered" => 4, _ => 3 };
var deadline = DateTime.UtcNow.AddSeconds(mode == "unknown" ? 2 : 10);
while (collected.Count < expected && DateTime.UtcNow < deadline)
{
    await Task.Delay(50);
}

Console.WriteLine($"collected {collected.Count}");
foreach (StoreChangedEvent delivery in collected)
{
    string line = $"{delivery.Change} {delivery.Name}";
    if (mode == "two")
    {
        line += $" exchange {delivery.ExchangeId}";
    }
    else if (mode == "sizes")
    {
        // The stored message's size, from the store: the owner and UID are in the event's Name.
        string[] parts = delivery.Name.Split('/');
        uint uid = uint.Parse(parts[2].Split('=')[1]);
        SurlMessagePage page = await server.Mail.ListMessagesAsync(parts[0], parts[1], uid - 1, 1, CancellationToken.None);
        line += $" ByteCount {(delivery.ByteCount == page.Messages[0].Size ? "equals" : "differs from")} the stored size";
    }

    Console.WriteLine(line);
}

return collected.Count == expected ? 0 : 1;
