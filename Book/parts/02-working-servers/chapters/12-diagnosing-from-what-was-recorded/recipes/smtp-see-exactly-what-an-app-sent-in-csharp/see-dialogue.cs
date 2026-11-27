// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of a shop app's shipping notice: the app sends it with Curl.Api to an in-process Surl
// server, the test waits for it in Ann's mailbox, and when it never comes the test prints the SMTP
// dialogue the server recorded, so the failure says where the mail went.
// Run: dotnet run see-dialogue.cs [-- <case>]
//   (no argument)  the app names the test address left in its settings; the test fails and prints the dialogue
//   landed         the same failure, then what each mailbox holds
//   login          the same failure, then the client's lines before MAIL FROM, from the record
//   log            the same failure, then the login lines of the server's verbose log
//   stored         the same failure, then the top of the copy the server stored
//   fixed          the app names Ann; the test passes
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "astray";
string recipient = mode == "fixed" ? "ann@example.com" : "test@example.com";

// SMTP on a free port for example.com, recording every exchange byte for byte.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, RecordExchanges = true, Log = log, LogLevel = LogLevel.Verbose };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("test@example.com:testpass");
await using SurlServer server = await SurlServer.StartAsync(options);

// The app: one notice to Ann, whose envelope names the address in the app's settings.
await using (CurlClient client = CurlClient.Create())
{
    CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}shop.example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
        Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [recipient] },
        Body = Encoding.ASCII.GetBytes(
            "From: app@example.com\r\nTo: ann@example.com\r\nSubject: Your order has shipped\r\n\r\nOrder 1042 left the warehouse today.\r\n"),
    }, CancellationToken.None);
    await sent.Body.DisposeAsync();
    Console.WriteLine($"SMTP {sent.StatusCode}");
}

// The client is disposed, so it has sent QUIT; wait for the server to end the exchange, with a bound.
await server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "smtp" }, TimeSpan.FromSeconds(10), CancellationToken.None);

// The test's assertion: the notice reaches Ann's mailbox within 2 seconds.
try
{
    await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "ann@example.com" }, TimeSpan.FromSeconds(2), CancellationToken.None);
}
catch (TimeoutException)
{
    Console.WriteLine("FAIL: no mail for ann@example.com within 2 s");
    switch (mode)
    {
        case "astray":
            Console.WriteLine("the dialogue from MAIL FROM:");
            Dialogue(from: "MAIL FROM").ForEach(Console.WriteLine);
            break;
        case "landed":
            foreach (string owner in new[] { "test@example.com", "ann@example.com" })
            {
                Console.WriteLine($"{owner} holds {(await server.Mail.ListMessagesAsync(owner, "INBOX")).Messages.Count}");
            }

            break;
        case "login":
            Console.WriteLine("before MAIL FROM:");
            Dialogue(from: "").TakeWhile(l => !l.StartsWith("C: MAIL FROM")).Where(l => l.StartsWith("C: ")).ToList().ForEach(Console.WriteLine);
            break;
        case "log":
            Console.WriteLine("the log:");
            log.ToString().Split('\n').Where(l => l.Contains("< AUTH CRAM-MD5") || l.Contains("< ****") || l.Contains("Login accepted"))
                .Select(l => l.Substring(l.IndexOf(' ') + 1).TrimEnd('\r')).ToList().ForEach(Console.WriteLine);
            break;
        case "stored":
            SurlMailMessage copy = await server.Mail.ReadMessageAsync("test@example.com", "INBOX", 1);
            Console.WriteLine("stored:");
            Encoding.ASCII.GetString(copy.Bytes.Span).Split("\r\n").Take(3).ToList().ForEach(Console.WriteLine);
            break;
    }

    return 1;
}

Console.WriteLine("PASS: ann@example.com has the notice");
Dialogue(from: "").Where(l => l.StartsWith("C: RCPT TO")).ToList().ForEach(Console.WriteLine);
return 0;

// The recorded SMTP exchange, a line per protocol line: C: what the client sent, S: what the server
// answered. With from, it starts at the first client line that begins with it.
List<string> Dialogue(string from)
{
    SurlExchangeRecord exchange = server.Exchanges.Last(e => e.Scheme == "smtp");
    var lines = new List<string>();
    bool on = from == "";
    foreach (SurlExchangeChunk chunk in exchange.Chunks)
    {
        string who = chunk.Direction == SurlExchangeDirection.Received ? "C" : "S";
        string text = Encoding.ASCII.GetString(chunk.Bytes.Span);
        foreach (string line in text.EndsWith("\r\n") ? text[..^2].Split("\r\n") : text.Split("\r\n"))
        {
            on = on || (who == "C" && line.StartsWith(from));
            if (on)
            {
                lines.Add($"{who}: {line}");
            }
        }
    }

    return lines;
}
