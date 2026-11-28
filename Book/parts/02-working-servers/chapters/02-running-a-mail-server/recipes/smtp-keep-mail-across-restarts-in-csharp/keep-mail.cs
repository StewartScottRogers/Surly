// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that mail survives a restart: an in-process Surl server keeps its mail store in DataDirectory,
// the test delivers over SMTP, stops the server, starts another on the same folder and reads the same message.
// Run: dotnet run keep-mail.cs [-- <case>]
//   (no argument)  deliver, stop, start again, read the message
//   on-disk        the folder's files, while the server runs and after it stops
//   uid            the next message, after the restart
//   seen           a flag set by a fetch before the restart
//   twice          two restarts, with a message in between
//   in-use         a second server on the folder while the first runs
//   memory         the same test with no DataDirectory
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "restart";

// The folder the mail store lives in: it must exist before the server starts.
DirectoryInfo data = Directory.CreateTempSubdirectory("surl-keep-");
string lockFile = Path.Combine(data.FullName, ".surl", "lock");
string indexFile = Path.Combine(data.FullName, ".surl", "mail", "index");

try
{
    SurlServer server = await Start(mode != "memory");

    // The shop mails Ann, and the test waits for the delivery event before it goes on.
    await Deliver(server, "Order 1042 has shipped");

    switch (mode)
    {
        case "on-disk":
            Console.WriteLine($"running: lock {File.Exists(lockFile)}, index {File.Exists(indexFile)}");
            await server.DisposeAsync();
            Console.WriteLine($"stopped: lock {File.Exists(lockFile)}, index {File.Exists(indexFile)}");
            break;
        case "in-use":
            try
            {
                await using SurlServer second = await Start(true);
                Console.WriteLine("second server: started");
            }
            catch (SurlServerStartException refused)
            {
                Console.WriteLine($"second server: {refused.ExitCode}, in use: {refused.Lines[0].Contains("is in use by another surl process")}");
            }

            await server.DisposeAsync();
            break;
        case "seen":
            // curl fetches with a plain UID FETCH, which sets \Seen; the store keeps the flag.
            Console.WriteLine($"unseen before the fetch: {await Unseen(server)}");
            await Fetch(server);
            Console.WriteLine($"unseen after the fetch: {await Unseen(server)}");
            await server.DisposeAsync();
            server = await Start(true);
            Console.WriteLine($"unseen after the restart: {await Unseen(server)}");
            await server.DisposeAsync();
            break;
        default:
            await server.DisposeAsync();
            server = await Start(mode != "memory");
            await Report(server, "after the restart");
            if (mode == "uid" || mode == "twice")
            {
                await Deliver(server, "Order 1043 has shipped");
                await Report(server, "after the next message");
            }

            if (mode == "twice")
            {
                await server.DisposeAsync();
                server = await Start(true);
                await Report(server, "after the second restart");
            }

            await server.DisposeAsync();
            break;
    }

    return 0;
}
finally
{
    data.Delete(true);
}

// The same options every time; the directory is what is kept.
async Task<SurlServer> Start(bool keep)
{
    var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" } };
    options.MailDomains.Add("example.com");
    options.Mailboxes.Add("shop@example.com:shoppass");
    options.Mailboxes.Add("ann@example.com:annpass");
    if (keep)
    {
        options.DataDirectory = data.FullName;
    }

    return await SurlServer.StartAsync(options);
}

// One message from the shop to Ann, sent with Curl.Api; delivery is asynchronous, so wait for its event, with a bound.
async Task Deliver(SurlServer server, string subject)
{
    var delivered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    using IDisposable watching = server.OnEvent(
        new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
        (e, cancellationToken) =>
        {
            delivered.TrySetResult(((StoreChangedEvent)e).Name);
            return ValueTask.CompletedTask;
        });

    await using CurlClient shop = CurlClient.Create();
    var mail = new CurlRequest(HttpMethod.Put, $"{server.BoundUrls[0]}example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "shop@example.com", Password = "shoppass" },
        Mail = new CurlMailOptions { MailFrom = "shop@example.com", MailRecipients = ["ann@example.com"] },
        Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(
            $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nYour order is on its way.\r\n"))),
    };
    CurlResponse sent = await shop.SendAsync(mail, CancellationToken.None);
    await sent.Body.DisposeAsync();
    if (await Task.WhenAny(delivered.Task, Task.Delay(TimeSpan.FromSeconds(10))) != delivered.Task)
    {
        throw new TimeoutException("no delivery within 10 s");
    }

    Console.WriteLine($"SMTP {sent.StatusCode}, delivered {delivered.Task.Result}");
}

// What Ann's INBOX holds, read from the store with no client: each message's UID and subject.
async Task Report(SurlServer server, string when)
{
    SurlMessagePage page = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX");
    var parts = new List<string>();
    foreach (SurlMessageSummary summary in page.Messages)
    {
        SurlMailMessage message = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", summary.Uid);
        string subject = Encoding.ASCII.GetString(message.Bytes.Span).Split("\r\n").First(l => l.StartsWith("Subject: "))["Subject: ".Length..];
        parts.Add($"UID {summary.Uid} {subject}");
    }

    Console.WriteLine($"{when}: {parts.Count} message(s){(parts.Count == 0 ? "" : ": " + string.Join(", ", parts))}");
}

async Task<int> Unseen(SurlServer server) => (await server.Mail.ListMailboxesAsync("ann@example.com")).Single(m => m.Name == "INBOX").UnseenCount;

// The app's own read over IMAP, as Ann.
async Task Fetch(SurlServer server)
{
    await using CurlClient ann = CurlClient.Create();
    var read = new CurlRequest(HttpMethod.Get, $"{server.BoundUrls[1]}INBOX;UID=1;SECTION=TEXT")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" },
    };
    CurlResponse response = await ann.SendAsync(read, CancellationToken.None);
    await response.Body.DisposeAsync();
}
