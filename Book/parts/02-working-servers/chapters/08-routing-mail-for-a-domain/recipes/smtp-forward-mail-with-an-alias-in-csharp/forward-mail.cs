// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app that mails a role address: an in-process Surl server forwards support@ to two
// mailboxes with an alias given through AdditionalArguments, the app sends with Curl.Api, and the test
// waits for the deliveries and reads both mailboxes from the store.
// Run: dotnet run forward-mail.cs [-- <case>]
//   (no argument)  the app mails support@; Ann and Bob each get it
//   property       the same alias, given through MailAliases
//   to-field       the To field of the stored copy
//   sender         the sending account's own mailbox
//   one-copy       one message to support@ and ann@ together
//   login          IMAP login as the alias
//   also-mailbox   an alias that is also a mailbox
//   no-mailbox     an alias whose target is no mailbox
//   outside        an alias whose target is outside the domain
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Collections.Concurrent;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "arguments";

// SMTP for the app and IMAP for a login test, serving example.com: the form's mailbox, Ann's and Bob's.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
foreach (string name in new[] { "form", "ann", "bob" })
{
    options.Mailboxes.Add($"{name}@example.com:{name}pass");
}

string alias = mode switch
{
    "also-mailbox" => "ann@example.com=bob@example.com",
    "no-mailbox" => "support@example.com=anne@example.com",
    "outside" => "support@example.com=ann@elsewhere.org",
    _ => "support@example.com=ann@example.com,bob@example.com",
};
if (mode == "property")
{
    options.MailAliases.Add(alias);
}
else
{
    options.AdditionalArguments.Add("--mail-alias");
    options.AdditionalArguments.Add(alias);
}

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"refused: {refused.ExitCode}: {refused.Lines[0]}");
    return 2;
}

await using (server)
{
    string smtp = $"{server.BoundUrls[0]}";
    string imap = $"{server.BoundUrls[1]}";
    var deliveries = new ConcurrentQueue<string>();
    var waiters = new ConcurrentQueue<(int Count, TaskCompletionSource Done)>();
    using IDisposable watching = server.OnEvent(
        new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
        (e, cancellationToken) =>
        {
            deliveries.Enqueue(((StoreChangedEvent)e).Name);
            foreach ((int count, TaskCompletionSource done) in waiters)
            {
                if (deliveries.Count >= count)
                {
                    done.TrySetResult();
                }
            }

            return ValueTask.CompletedTask;
        });

    await using CurlClient client = CurlClient.Create();

    if (mode == "login")
    {
        // An alias is an address, not an account: the server has no login for it.
        try
        {
            var read = new CurlRequest(HttpMethod.Get, $"{imap}INBOX?ALL")
            {
                Authentication = new CurlAuthenticationOptions { UserName = "support@example.com", Password = "annpass" },
            };
            await client.SendAsync(read, CancellationToken.None);
            Console.WriteLine("login as support@example.com: accepted");
            return 1;
        }
        catch (CurlException denied)
        {
            Console.WriteLine($"login as support@example.com: curl error {(int)denied.Code} {denied.Code}");
            return 0;
        }
    }

    // The app: one mail to support@, as the form, then (in one-copy) a second to support@ and ann@ together.
    Console.WriteLine($"support@: SMTP {await Send(["support@example.com"])}");
    if (!await Delivered(2))
    {
        return 1;
    }

    if (mode == "one-copy")
    {
        Console.WriteLine($"support@ and ann@: SMTP {await Send(["support@example.com", "ann@example.com"])}");

        // Deliveries happen in order. A marker for the form, delivered last, shows nothing is still on its way.
        Console.WriteLine($"marker: SMTP {await Send(["form@example.com"])}");
        if (!await Delivered(5))
        {
            return 1;
        }
    }

    Console.WriteLine($"delivered: {string.Join(", ", deliveries.Take(2).OrderBy(n => n))}");

    // The test: what each mailbox holds, read from the store with no client.
    switch (mode)
    {
        case "to-field":
            SurlMailMessage copy = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", 1);
            Console.WriteLine(Header(copy, "To: "));
            Console.WriteLine($"mentions ann or bob: {Encoding.ASCII.GetString(copy.Bytes.Span).Contains("ann@example.com") || Encoding.ASCII.GetString(copy.Bytes.Span).Contains("bob@example.com")}");
            break;
        case "sender":
            Console.WriteLine($"form@example.com holds {await Count("form@example.com")}");
            break;
        case "one-copy":
            Console.WriteLine($"ann@example.com holds {await Count("ann@example.com")}");
            Console.WriteLine($"bob@example.com holds {await Count("bob@example.com")}");
            Console.WriteLine($"form@example.com holds {await Count("form@example.com")}");
            break;
        default:
            foreach (string owner in new[] { "ann@example.com", "bob@example.com" })
            {
                Console.WriteLine($"{owner}: {Header(await server.Mail.ReadMessageAsync(owner, "INBOX", 1), "Subject: ")["Subject: ".Length..]}");
            }

            break;
    }

    return 0;

    // Waits, with a bound, until the store has had this many deliveries in all.
    async Task<bool> Delivered(int count)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        waiters.Enqueue((count, done));
        if (deliveries.Count >= count)
        {
            done.TrySetResult();
        }

        if (await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(10))) == done.Task)
        {
            return true;
        }

        Console.WriteLine($"only {deliveries.Count} of {count} deliveries within 10 s");
        return false;
    }
}

// One mail from the form: Curl.Api sends SMTP with a stream body, logging in with CRAM-MD5.
async Task<int> Send(string[] to)
{
    await using CurlClient app = CurlClient.Create();
    var mail = new CurlRequest(HttpMethod.Put, $"{server.BoundUrls[0]}example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "form@example.com", Password = "formpass" },
        Mail = new CurlMailOptions { MailFrom = "form@example.com", MailRecipients = to },
        Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(
            $"From: form@example.com\r\nTo: {to[0]}\r\nSubject: Cannot log in\r\n\r\nPlease reset my password.\r\n"))),
    };
    CurlResponse sent = await app.SendAsync(mail, CancellationToken.None);
    await sent.Body.DisposeAsync();
    return sent.StatusCode;
}

async Task<int> Count(string owner) => (await server.Mail.ListMessagesAsync(owner, "INBOX")).Messages.Count;

// The value of one header field of a stored message.
string Header(SurlMailMessage message, string name) =>
    Encoding.ASCII.GetString(message.Bytes.Span).Split("\r\n").First(line => line.StartsWith(name));
