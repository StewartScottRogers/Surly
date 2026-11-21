// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that the app's sender connects again after a dropped connection and that one copy of the mail
// arrives: an in-process Surl server drops the first SMTP connection (or the first two, or one at DATA
// or at QUIT), the app sends with Curl.Api and tries again on an error, and the test reads the store.
// Run: dotnet run resend.cs [-- <case>]
//   (no argument)  the first connection is dropped before the greeting; the sender tries once more
//   log            the same, and the server's log lines: connections opened and messages stored
//   twice          the first two connections are dropped: a send with no retry, then one with one retry
//   data           the first DATA command drops the connection, before the message is accepted
//   quit           the first QUIT drops the connection, after the message was accepted
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "greeting";

// SMTP on a free port for example.com, the app's mailbox and ann's, and one drop rule.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, Log = log, LogLevel = LogLevel.Verbose };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Faults.Add(mode switch
{
    "twice" => SurlFault.DropConnection() with { Scheme = "smtp", Times = 2 },
    "data" => SurlFault.DropConnection() with { Scheme = "smtp", Stage = FaultStage.Command, Command = "DATA", Times = 1 },
    "quit" => SurlFault.DropConnection() with { Scheme = "smtp", Stage = FaultStage.Command, Command = "QUIT", Times = 1 },
    _ => SurlFault.DropConnection() with { Scheme = "smtp", Times = 1 },
});
await using SurlServer server = await SurlServer.StartAsync(options);

// Delivery is asynchronous: every delivery is an event, and a wait for the next one has a bound.
SemaphoreSlim arrived = new(0);
using IDisposable watching = server.OnEvent(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    (e, cancellationToken) =>
    {
        arrived.Release();
        return ValueTask.CompletedTask;
    });

// The app's sender: up to 1 + retries tries, each on a new connection, the next one after any error, one second later.
await using CurlClient client = CurlClient.Create();
async Task<bool> Send(string label, int retries)
{
    for (int attempt = 1; attempt <= 1 + retries; attempt++)
    {
        var send = new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["ann@example.com"] },
            Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes(
                "From: app@example.com\r\nTo: ann@example.com\r\nSubject: Order 1042 has shipped\r\n\r\nYour order is on its way.\r\n"))),
        };
        try
        {
            CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
            await sent.Body.DisposeAsync();
            Console.WriteLine($"{label}, try {attempt}: SMTP {sent.StatusCode}, {attempt - 1} retries");
            return await arrived.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (CurlException dropped)
        {
            Console.WriteLine($"{label}, try {attempt}: curl error {(int)dropped.Code} {dropped.Code}");
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    return true;
}

bool delivered;
if (mode == "twice")
{
    await Send("first send", 0);
    Console.WriteLine($"ann@example.com holds {await Held()}");
    delivered = await Send("second send", 1);
}
else
{
    delivered = await Send("send", 1);
}

if (!delivered)
{
    Console.WriteLine("no delivery within 10 s");
    return 1;
}

// The test: what the store holds, and what the server's log says, read with no client.
Console.WriteLine($"ann@example.com holds {await Held()}");
if (mode == "log")
{
    string text = log.ToString();
    Console.WriteLine($"connections logged: {Regex.Matches(text, "opened: smtp").Count}");
    Console.WriteLine($"stored logged: {Regex.Matches(text, "Message stored: ").Count}");
}

return 0;

async Task<int> Held() => (await server.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages.Count;
