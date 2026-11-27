// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test that an app cannot mail outside its domains: an in-process surl serves example.com and
// example.org, the app's sends go through Curl.Api, and the refusal is found in the recorded exchange.
// Run: dotnet run foreign-mail.cs
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

// SMTP on a free port for two domains; exchanges recorded, a verbose log kept in memory.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0" },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
options.MailDomains.Add("example.com");
options.MailDomains.Add("example.org");
options.Mailboxes.Add("billing@example.com:billpass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("sales@example.org:salespass");

await using var server = await SurlServer.StartAsync(options);
string smtp = $"{server.BoundUrls[0]}";
const string Invoice = "From: billing@example.com\r\nTo: ann@example.com\r\nSubject: Your invoice\r\n\r\nInvoice 1042 is attached.\r\n";
const string CcOutside = "From: billing@example.com\r\nTo: ann@example.com\r\nCc: carol@elsewhere.org\r\nSubject: Your invoice\r\n\r\nInvoice 1043 is attached.\r\n";
long lastSequence = 0;

// The app's send to an outside address; then what the server answered its RCPT TO, from the record.
Console.WriteLine($"carol@elsewhere.org: {await Send("billing@example.com", "carol@elsewhere.org", Invoice)}");
Console.WriteLine($"recorded: {Reply(server.Exchanges[^1])}");

Console.WriteLine($"sales@example.org: {await Send("billing@example.com", "sales@example.org", Invoice)}");
Console.WriteLine($"ann@mail.example.com: {await Send("billing@example.com", "ann@mail.example.com", Invoice)}");
Console.WriteLine($"ann@example.com.evil.org: {await Send("billing@example.com", "ann@example.com.evil.org", Invoice)}");

// Letter case: accepted, and the delivery into ann's mailbox comes within ten seconds.
Console.WriteLine($"ANN@EXAMPLE.COM: {await Send("billing@example.com", "ANN@EXAMPLE.COM", Invoice)}");
SurlEvent delivered = await server.WaitForEventAsync(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "ann@example.com" }, TimeSpan.FromSeconds(10));
Console.WriteLine($"delivered: {((StoreChangedEvent)delivered).Name}");

// Only the envelope's recipients are judged: not the sender, not the header fields.
Console.WriteLine($"from noreply@elsewhere.org: {await Send("noreply@elsewhere.org", "ann@example.com", Invoice)}");
Console.WriteLine($"Cc carol@elsewhere.org, sent to ann: {await Send("billing@example.com", "ann@example.com", CcOutside)}");

// The log names each refusal and its reason; StopAsync ends every exchange first.
await server.StopAsync();
foreach (string line in log.ToString().Split('\n').Select(l => l.TrimEnd()).Where(l => l.Contains(" refused: ")))
{
    Console.WriteLine($"log: {line[(line.IndexOf(' ') + 1)..]}");
}

return 0;

// The app's send, on a fresh CurlClient; then a bounded wait until surl has ended the exchange,
// so the record is complete before anyone reads it.
async Task<string> Send(string sender, string recipient, string message)
{
    string outcome;
    await using (CurlClient client = CurlClient.Create())
    {
        try
        {
            CurlResponse sent = await client.SendAsync(new CurlRequest(HttpMethod.Put, smtp + "app.example.com")
            {
                Mail = new CurlMailOptions { MailFrom = sender, MailRecipients = [recipient] },
                Authentication = new CurlAuthenticationOptions { UserName = "billing@example.com", Password = "billpass" },
                Body = Encoding.ASCII.GetBytes(message),
            }, CancellationToken.None);
            outcome = $"{sent.StatusCode}";
        }
        catch (CurlException refused)
        {
            outcome = $"CurlException {refused.Code} ({(int)refused.Code}): {refused.Message}";
        }
    }

    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, Scheme = "smtp", AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
    return outcome;
}

// The client's RCPT TO line and the server's line after it.
static string Reply(SurlExchangeRecord exchange)
{
    string[] lines = exchange.Chunks.Select(c => Encoding.ASCII.GetString(c.Bytes.Span).TrimEnd()).ToArray();
    int rcpt = Array.FindIndex(lines, l => l.StartsWith("RCPT TO:"));
    return $"{lines[rcpt]} -> {lines[rcpt + 1]}";
}
