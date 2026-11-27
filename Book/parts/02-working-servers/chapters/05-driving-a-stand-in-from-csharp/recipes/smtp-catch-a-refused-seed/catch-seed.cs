// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test seeds an order confirmation into a mailbox before it runs. The address has a typo, and
// the seed throws SurlSeedRefusedException, where the same mail over SMTP would vanish unseen.
// Run: dotnet run catch-seed.cs [-- <case>]
//   (no argument)  seed for anne@example.com, which has no mailbox
//   outside        seed for ann@example.org, outside the mail domain
//   malformed      seed for "ann", which is not an address
//   smtp           send the same mail for anne@example.com over SMTP instead
//   right          seed for ann@example.com
//   catch-all      seed for anne@example.com with a catch-all mailbox for example.com
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "typo";

// SMTP on a free port for example.com, with Ann's mailbox, the shop's, and a postmaster's.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("shop@example.com:shoppass");
options.Mailboxes.Add("postmaster@example.com:postpass");
if (mode == "catch-all")
{
    options.MailCatchAlls.Add("example.com=postmaster@example.com");
}

await using SurlServer server = await SurlServer.StartAsync(options);

// Every change to the mail store, as a witness: a refused seed changes nothing.
int changes = 0;
using IDisposable witness = server.OnEvent(new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    (e, ct) => { Interlocked.Increment(ref changes); return ValueTask.CompletedTask; });

string to = mode switch
{
    "outside" => "ann@example.org",
    "malformed" => "ann",
    "right" => "ann@example.com",
    _ => "anne@example.com",
};
byte[] message = Encoding.ASCII.GetBytes(
    "From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order 1042 confirmed\r\n\r\nThank you for your order.\r\n");

if (mode == "smtp")
{
    // The same mail as the app would send it: SMTP answers 250 and the mail goes nowhere.
    await using CurlClient client = CurlClient.Create();
    CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}shop.example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "shop@example.com", Password = "shoppass" },
        Mail = new CurlMailOptions { MailFrom = "shop@example.com", MailRecipients = [to] },
        Body = message,
    }, CancellationToken.None);
    await sent.Body.DisposeAsync();
    Console.WriteLine($"SMTP {sent.StatusCode} for {to}");
}
else
{
    // The seed: delivered as SMTP would deliver it after RCPT TO, or refused with a reason.
    try
    {
        await server.Mail.DeliverAsync(to, message, CancellationToken.None);
        Console.WriteLine($"seeded for {to}");
    }
    catch (SurlSeedRefusedException refused)
    {
        Console.WriteLine($"refused: Store {refused.Store}, Refusal {refused.Refusal}");
        Console.WriteLine(refused.Message);
    }
}

// What the mail store holds now, and how many changes it saw, after a moment for any delivery.
await Task.Delay(TimeSpan.FromSeconds(1));
foreach (string owner in new[] { "ann@example.com", "postmaster@example.com" })
{
    Console.WriteLine($"{owner} holds {(await server.Mail.ListMessagesAsync(owner, "INBOX", 0, 100, CancellationToken.None)).Messages.Count}");
}

Console.WriteLine($"store changes: {changes}");
return 0;
