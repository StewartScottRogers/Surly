// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An app mails the minutes to a list in which one address is outside the server's domain. An
// in-process Surl server takes the mail; the app sends with Curl.Api and reports what was refused.
// Run: dotnet run send-minutes.cs [-- <case>]
//   (no argument)  one request per recipient: three are sent, the refused one is reported
//   allowfails     one request for all four, with MailRecipientAllowFails
//   which          the same, then the server's answer to each RCPT TO, from its record
//   log            the same, then the server's log lines for the refusal and the stored message
//   headers        the same, then the To header of Ann's copy
//   without        one request for Dee and Carol, without MailRecipientAllowFails
//   all-refused    one request for two outside addresses, with MailRecipientAllowFails
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "report";

// SMTP on a free port for example.com only, recording each exchange and logging verbosely.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, RecordExchanges = true, Log = log, LogLevel = LogLevel.Verbose };
options.MailDomains.Add("example.com");
foreach (string name in new[] { "app", "ann", "bob", "cy", "dee" })
{
    options.Mailboxes.Add($"{name}@example.com:{name}pass");
}

await using SurlServer server = await SurlServer.StartAsync(options);

// Every delivery is an event; the program waits for the one it expects last, with a bound.
var deliveries = new ConcurrentQueue<string>();
var cyDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
using IDisposable watching = server.OnEvent(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    (e, cancellationToken) =>
    {
        string name = ((StoreChangedEvent)e).Name;
        deliveries.Enqueue(name);
        if (name.StartsWith("cy@example.com/"))
        {
            cyDelivered.TrySetResult();
        }

        return ValueTask.CompletedTask;
    });

string[] list = ["ann@example.com", "carol@elsewhere.org", "bob@example.com", "cy@example.com"];
byte[] minutes = Encoding.ASCII.GetBytes(
    "From: app@example.com\r\nTo: ann@example.com, carol@elsewhere.org, bob@example.com, cy@example.com\r\n" +
    "Subject: Minutes of Tuesday's meeting\r\n\r\nThe minutes are on the team site.\r\n");
await using CurlClient client = CurlClient.Create();

switch (mode)
{
    case "report":
        // One request per recipient: each answer is that recipient's own.
        foreach (string to in list)
        {
            Console.WriteLine($"{to}: {await Send([to], allowFails: false)}");
        }

        break;
    case "without":
        Console.WriteLine(await Send(["dee@example.com", "carol@elsewhere.org"], allowFails: false));
        Console.WriteLine($"dee@example.com holds {(await server.Mail.ListMessagesAsync("dee@example.com", "INBOX")).Messages.Count}");
        return 0;
    case "all-refused":
        Console.WriteLine(await Send(["carol@elsewhere.org", "dan@elsewhere.org"], allowFails: true));
        return 0;
    default:
        Console.WriteLine(await Send(list, allowFails: true));
        break;
}

if (await Task.WhenAny(cyDelivered.Task, Task.Delay(TimeSpan.FromSeconds(10))) != cyDelivered.Task)
{
    Console.WriteLine("no delivery to cy@example.com within 10 s");
    return 1;
}

Console.WriteLine($"delivered: {string.Join(", ", deliveries.Order())}");
switch (mode)
{
    case "which":
        // The server's record of the exchange, read once the client has said QUIT and the server has
        // ended the exchange: the reply that follows each RCPT TO.
        await client.DisposeAsync();
        await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "smtp" }, TimeSpan.FromSeconds(10), CancellationToken.None);
        string[] lines = server.Exchanges.Last(x => x.Scheme == "smtp").Chunks
            .SelectMany(c => Encoding.ASCII.GetString(c.Bytes.Span).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)).ToArray();
        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].StartsWith("RCPT TO:"))
            {
                Console.WriteLine($"{lines[i]} -> {lines[i + 1]}");
            }
        }

        break;
    case "log":
        // The log line can trail the delivery by a moment: look again until it is there, for up to 10 s.
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!log.ToString().Contains("for 3 recipients") && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        foreach (Match m in Regex.Matches(log.ToString(), @"\* (RCPT \S+ refused: .*|Message stored: \d+ bytes for 3 recipients)"))
        {
            Console.WriteLine(m.Groups[1].Value.TrimEnd('\r'));
        }

        break;
    case "headers":
        SurlMailMessage copy = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", 1);
        Console.WriteLine(Encoding.ASCII.GetString(copy.Bytes.Span).Split("\r\n").First(l => l.StartsWith("To: ")));
        break;
}

return 0;

// One send: the server's last reply, or what the CurlException says.
async Task<string> Send(string[] to, bool allowFails)
{
    try
    {
        CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = to, MailRecipientAllowFails = allowFails },
            Body = minutes,
        }, CancellationToken.None);
        await sent.Body.DisposeAsync();
        return $"{sent.StatusCode}";
    }
    catch (CurlException refused)
    {
        return $"refused, curl error {(int)refused.Code} {refused.Code}: {refused.Message}";
    }
}
