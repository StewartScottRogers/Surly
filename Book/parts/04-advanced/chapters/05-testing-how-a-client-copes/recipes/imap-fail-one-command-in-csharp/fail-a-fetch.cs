// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of how a mail app copes with one IMAP command answered NO: an in-process Surl server fails a
// command on purpose, and the app's fetch, written with Curl.Api, meets the refusal.
// Run: dotnet run fail-a-fetch.cs [-- <case>]
//   (no argument)  the first fetch is answered NO; the same fetch again works
//   search         a SEARCH works while the rule for the fetch still waits
//   retry          the app tries again at once and gets the message on the second try
//   no-retry       the app gives up on the first refusal
//   select         the rule is for SELECT, so the failure comes before any fetch
//   log            the server's own log of the refusal
//   reply          the server's side of the refused fetch, from the recorded exchange
//   count          how often the rule acted, from the rule and from the events
//   late-rule      no rule at start; one is added during the test, then removed
//   bad-shape      a rule whose reply is not in IMAP's shape
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "first-fails";

// The server: IMAP on a free port for example.com, one mailbox, and (unless the case says otherwise) one rule.
var log = new StringWriter();
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" }, RecordExchanges = true, Log = log, LogLevel = LogLevel.Verbose };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
if (mode == "bad-shape")
{
    options.Faults.Add(SurlFault.ProtocolError("250 OK") with { Scheme = "imap", Command = "FETCH" });
}
else if (mode != "late-rule")
{
    string command = mode == "select" ? "SELECT" : "FETCH";
    string reply = mode == "select" ? "NO [UNAVAILABLE] store busy" : "NO [SERVERBUG] fetch failed";
    options.Faults.Add(SurlFault.ProtocolError(reply) with { Scheme = "imap", Command = command, Times = 1 });
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
    // The mail the app will fetch, put in the store with no client.
    await server.Mail.DeliverAsync("ann@example.com",
        "From: shop@example.com\r\nTo: ann@example.com\r\nSubject: Order 1042 has shipped\r\n\r\nYour order is on its way.\r\n"u8.ToArray(),
        CancellationToken.None);

    switch (mode)
    {
        case "search":
            Console.WriteLine($"search: {await Read("INBOX?ALL")}");
            Console.WriteLine($"fetch: {await Try("INBOX;UID=1;SECTION=TEXT")}");
            break;
        case "retry":
            // The app's own loop: up to three tries, straight away.
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                string outcome = await Try("INBOX;UID=1;SECTION=TEXT");
                Console.WriteLine($"try {attempt}: {outcome}");
                if (!outcome.StartsWith("failed"))
                {
                    break;
                }
            }

            break;
        case "no-retry":
            string only = await Try("INBOX;UID=1;SECTION=TEXT");
            Console.WriteLine($"try 1: {only}");
            Console.WriteLine($"messages: {(await server.Mail.ListMailboxesAsync("ann@example.com")).Single().MessageCount}");
            return only.StartsWith("failed") ? 1 : 0;
        case "select":
            Console.WriteLine($"search: {await Try("INBOX?ALL")}");
            Console.WriteLine($"search again: {await Try("INBOX?ALL")}");
            break;
        case "reply":
            await Try("INBOX;UID=1;SECTION=TEXT");
            // The server records an exchange a moment after the client has its answer: look again until
            // every exchange that started has ended, for up to 10 s.
            for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
            {
                if (server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted }).Count == server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).Count) break;
            }

            string sent =string.Concat(server.Exchanges.Select(e => Encoding.ASCII.GetString(e.Sent)));
            foreach (string line in sent.Split("\r\n").Where(l => Regex.IsMatch(l, "^A00[34] ")))
            {
                Console.WriteLine(line);
            }

            break;
        case "log":
            await Try("INBOX;UID=1;SECTION=TEXT");
            foreach (string line in log.ToString().Split('\n').Where(l => l.Contains("fault")))
            {
                Console.WriteLine(Regex.Replace(line.Trim(), @"^#\d+ ", ""));
            }

            break;
        case "count":
            await Try("INBOX;UID=1;SECTION=TEXT");
            await Try("INBOX;UID=1;SECTION=TEXT");
            SurlFaultRule rule = server.Faults.List().Single();
            Console.WriteLine($"rule {rule.Id} remaining: {rule.Remaining}");
            Console.WriteLine($"fault applied: {server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count}");
            break;
        case "late-rule":
            Console.WriteLine($"before: {await Try("INBOX;UID=1;SECTION=TEXT")}");
            string id = server.Faults.Add(SurlFault.ProtocolError("NO [SERVERBUG] fetch failed") with { Scheme = "imap", Command = "FETCH" });
            Console.WriteLine($"with the rule: {await Try("INBOX;UID=1;SECTION=TEXT")}");
            Console.WriteLine($"with the rule again: {await Try("INBOX;UID=1;SECTION=TEXT")}");
            Console.WriteLine($"removed {id}: {server.Faults.Remove(id)}");
            Console.WriteLine($"after: {await Try("INBOX;UID=1;SECTION=TEXT")}");
            break;
        default:
            Console.WriteLine($"fetch 1: {await Try("INBOX;UID=1;SECTION=TEXT")}");
            Console.WriteLine($"fetch 2: {await Try("INBOX;UID=1;SECTION=TEXT")}");
            break;
    }

    return 0;
}

// One IMAP read as ann, on a client of its own: a CurlClient keeps its session and its selected folder.
async Task<string> Read(string path)
{
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" },
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).Trim();
}

// The same, with a refusal turned into text: the exception type, curl's code and the message.
async Task<string> Try(string path)
{
    try
    {
        return await Read(path);
    }
    catch (CurlException e)
    {
        return $"failed {(int)e.Code} {e.Code}: {e.Message}";
    }
}
