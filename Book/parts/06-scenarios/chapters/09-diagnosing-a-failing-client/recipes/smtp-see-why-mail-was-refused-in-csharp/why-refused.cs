// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that finds why an app's mail was refused: the app sends through Curl.Api and fails; the test
// waits for the SMTP exchange to end and reads the refused command and reply from Surl.Api's record,
// and surl's reason from its log.
// Run: dotnet run why-refused.cs
#:package Surly.Surl.Api@0.1.13
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

// SMTP on a free port; every exchange recorded, the verbose log kept. ops@example.com holds at most 200 bytes.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0" },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("ops@example.com:opspass;quota=200");
options.Mailboxes.Add("app@example.com:apppass");
await using var server = await SurlServer.StartAsync(options);
string smtp = $"{server.BoundUrls[0]}";
byte[] report = Encoding.ASCII.GetBytes("From: app@example.com\r\nTo: ann@example.com\r\nSubject: Nightly report\r\n\r\nAll 42 jobs finished. None failed.\r\n");
long lastSequence = 0;

// The app: one send on a fresh CurlClient. It would swallow the exception; the test prints it.
async Task<string> App(string to, string? password = "apppass")
{
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, smtp + "app.example.com")
        {
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [to] },
            Authentication = password is null ? null : new CurlAuthenticationOptions { UserName = "app@example.com", Password = password },
            Body = report,
        }, CancellationToken.None);
        return $"{sent.StatusCode}";
    }
    catch (CurlException failed)
    {
        return $"CurlException {failed.Code} ({(int)failed.Code}): {failed.Message}";
    }
}

// The diagnosis: wait (at most 10 s) until the exchange has ended, then each reply of 4xx or 5xx with the
// client's line before it, from the record, and surl's reason, from the log lines of that exchange.
async Task<string> WhyRefused()
{
    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
    SurlExchangeRecord exchange = server.Exchanges[^1];
    var found = new List<string>();
    string lastReceived = "";
    foreach (SurlExchangeChunk chunk in exchange.Chunks)
    {
        string line = Encoding.ASCII.GetString(chunk.Bytes.Span).TrimEnd();
        if (chunk.Direction == SurlExchangeDirection.Received)
        {
            lastReceived = line.Split("\r\n")[^1];
        }
        else if (line.Length > 3 && line[0] is '4' or '5')
        {
            found.Add($"{lastReceived} -> {line}");
        }
    }

    string reason = log.ToString().Split('\n').Select(l => l.TrimEnd())
        .LastOrDefault(l => l.StartsWith($"#{exchange.ExchangeId} * ") && l.Contains(" refused")) ?? "";
    return found.Count == 0 ? "no refusal" : $"{string.Join("; ", found)}\n  reason: {reason[(reason.IndexOf('*') + 2)..]}";
}

async Task<int> Count(string owner) => (await server.Mail.ListMailboxesAsync(owner)).Single(m => m.Name == "INBOX").MessageCount;

// The failure as the app's owner meets it, then the diagnosis.
Console.WriteLine($"app, to ann@example.org: {await App("ann@example.org")}");
Console.WriteLine($"ann's INBOX holds {await Count("ann@example.com")}");
Console.WriteLine($"refused: {await WhyRefused()}");

// The fix, and the report arriving: delivery is asynchronous, so wait for its event first.
Console.WriteLine($"app, to ann@example.com: {await App("ann@example.com")}; {await WhyRefused()}");
await server.WaitForEventAsync(new SurlEventQuery { Store = StoreKind.Mail, Name = "ann@example.com/INBOX/;UID=1" }, TimeSpan.FromSeconds(10));
Console.WriteLine($"ann's INBOX holds {await Count("ann@example.com")}");

// The other ways a send fails.
Console.WriteLine($"no login: {await App("ann@example.com", password: null)}");
Console.WriteLine($"  refused: {await WhyRefused()}");
Console.WriteLine($"wrong password: {await App("ann@example.com", password: "wrongpass")}");
Console.WriteLine($"  refused: {await WhyRefused()}");
Console.WriteLine($"to a full mailbox: {await App("ops@example.com")}");
Console.WriteLine($"  refused: {await WhyRefused()}");
return 0;
