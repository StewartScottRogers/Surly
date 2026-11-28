// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test server for an app that serves two brands, one.test and two.test: an in-process Surl server
// takes mail for both, each brand sends with Curl.Api, and the test checks each mailbox in the store.
// Run: dotnet run serve-brands.cs [-- <case>]
//   (no argument)  each brand sends its welcome to Ann in its own domain
//   own-password   an IMAP login as ann@two.test with ann@one.test's password
//   bare-name      an IMAP login as ann
//   third          a send to ann@three.test, then the server's log line for it
//   subdomain      a send to ann@mail.one.test
//   unknown        a send to cy@two.test, which has no mailbox, then the server's log line for it
//   both           one send to Bo in both domains
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Collections.Concurrent;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "welcome";

// SMTP and IMAP on free ports, for two domains: a noreply mailbox per brand, and Ann and Bo in each.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" }, Log = log, LogLevel = LogLevel.Verbose };
options.MailDomains.Add("one.test");
options.MailDomains.Add("two.test");
foreach (string mailbox in new[] { "noreply@one.test:onepass", "noreply@two.test:twopass", "ann@one.test:annone", "ann@two.test:anntwo", "bo@one.test:boone", "bo@two.test:botwo" })
{
    options.Mailboxes.Add(mailbox);
}

await using SurlServer server = await SurlServer.StartAsync(options);
string smtp = $"{server.BoundUrls[0]}";
string imap = $"{server.BoundUrls[1]}";

// Every delivery is an event; the program waits for those it expects, with a bound.
var delivered = new ConcurrentQueue<string>();
using IDisposable watching = server.OnEvent(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    (e, cancellationToken) =>
    {
        delivered.Enqueue(((StoreChangedEvent)e).Name);
        return ValueTask.CompletedTask;
    });

switch (mode)
{
    case "own-password":
        Console.WriteLine(await Login("ann@two.test", "annone"));
        break;
    case "bare-name":
        Console.WriteLine(await Login("ann", "annone"));
        break;
    case "third":
        Console.WriteLine(await Send("two", ["ann@three.test"]));
        Console.WriteLine(await LogLine("ann@three.test refused"));
        break;
    case "subdomain":
        Console.WriteLine(await Send("one", ["ann@mail.one.test"]));
        break;
    case "unknown":
        Console.WriteLine(await Send("two", ["cy@two.test"]));
        Console.WriteLine(await LogLine("cy@two.test discarded"));
        break;
    case "both":
        Console.WriteLine(await Send("one", ["bo@one.test", "bo@two.test"]));
        await Delivered("bo@one.test", "bo@two.test");
        await Report("bo@one.test", "bo@two.test");
        break;
    default:
        Console.WriteLine(await Send("one", ["ann@one.test"]));
        Console.WriteLine(await Send("two", ["ann@two.test"]));
        await Delivered("ann@one.test", "ann@two.test");
        await Report("ann@one.test", "ann@two.test");
        break;
}

return 0;

// A brand's welcome, sent as that brand's noreply mailbox with a client of its own, since each brand
// logs in as itself: the server's last reply, or what the CurlException says.
async Task<string> Send(string brand, string[] to)
{
    await using CurlClient client = CurlClient.Create();
    string from = $"noreply@{brand}.test";
    byte[] welcome = Encoding.ASCII.GetBytes(
        $"From: {from}\r\nTo: {to[0]}\r\nSubject: Welcome to {(brand == "one" ? "One" : "Two")}\r\n\r\nThanks for joining.\r\n");
    try
    {
        CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{smtp}{brand}.test")
        {
            Authentication = new CurlAuthenticationOptions { UserName = from, Password = $"{brand}pass" },
            Mail = new CurlMailOptions { MailFrom = from, MailRecipients = to },
            Body = welcome,
        }, CancellationToken.None);
        await sent.Body.DisposeAsync();
        return $"SMTP {sent.StatusCode}";
    }
    catch (CurlException refused)
    {
        return $"refused: curl error {(int)refused.Code} {refused.Code}: {refused.Message}";
    }
}

// An IMAP login through Curl.Api, with a client of its own: a SEARCH of the INBOX.
async Task<string> Login(string user, string password)
{
    await using CurlClient reader = CurlClient.Create();
    try
    {
        CurlResponse found = await reader.SendAsync(new CurlRequest(HttpMethod.Get, $"{imap}INBOX?ALL")
        {
            Authentication = new CurlAuthenticationOptions { UserName = user, Password = password },
        }, CancellationToken.None);
        return $"logged in: {(await new StreamReader(found.Body).ReadToEndAsync()).Trim()}";
    }
    catch (CurlException refused)
    {
        return $"refused: curl error {(int)refused.Code} {refused.Code}";
    }
}

// Waits, for at most 10 seconds, until each named mailbox has had a delivery.
async Task Delivered(params string[] owners)
{
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); !owners.All(o => delivered.Any(n => n.StartsWith(o + "/"))); await Task.Delay(50))
    {
        if (waited.Elapsed > TimeSpan.FromSeconds(10))
        {
            throw new TimeoutException($"no delivery to all of {string.Join(", ", owners)} within 10 s");
        }
    }

    Console.WriteLine($"delivered: {string.Join(", ", delivered.Order())}");
}

// What each mailbox holds, read from the store: the count, and the subject of UID 1.
async Task Report(params string[] owners)
{
    foreach (string owner in owners)
    {
        SurlMessagePage page = await server.Mail.ListMessagesAsync(owner, "INBOX");
        SurlMailMessage first = await server.Mail.ReadMessageAsync(owner, "INBOX", 1);
        string subject = Encoding.ASCII.GetString(first.Bytes.Span).Split("\r\n").First(l => l.StartsWith("Subject: "));
        Console.WriteLine($"{owner} holds {page.Messages.Count}: {subject}");
    }
}

// Waits, for at most 10 seconds, for a line of the verbose log holding text, and returns it.
async Task<string> LogLine(string text)
{
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); waited.Elapsed < TimeSpan.FromSeconds(10); await Task.Delay(50))
    {
        string? line = log.ToString().Split('\n').FirstOrDefault(l => l.Contains(text));
        if (line != null)
        {
            return line[(line.IndexOf(' ') + 1)..].TrimEnd('\r');
        }
    }

    return $"no log line with {text} within 10 s";
}
