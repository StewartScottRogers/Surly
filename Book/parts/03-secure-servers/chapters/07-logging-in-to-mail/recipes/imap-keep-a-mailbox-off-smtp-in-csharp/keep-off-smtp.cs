// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test mailbox that may be read over IMAP but never log in to send mail: an in-process Surl server
// with the mailbox restricted by protocols=imap, and Curl.Api clients that try it on each protocol.
// Run: dotnet run keep-off-smtp.cs [-- <case>]
//   (no argument)  the viewer on IMAP, SMTP and POP3; app, which has no restriction, on all three
//   wrong          the viewer's IMAP login with a wrong password
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "all";

// IMAP, SMTP and POP3 over one mail store. viewer is kept to IMAP; app has no protocols= list.
var options = new SurlServerOptions
{
    ListenUrls = { "imap://127.0.0.1:0", "smtp://127.0.0.1:0", "pop3://127.0.0.1:0" },
    RecordExchanges = true,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("viewer@example.com:viewpass;protocols=imap");
options.Mailboxes.Add("app@example.com:apppass");
await using var server = await SurlServer.StartAsync(options);
string imap = $"{server.BoundUrls[0]}", smtp = $"{server.BoundUrls[1]}", pop3 = $"{server.BoundUrls[2]}";

// Mail arrives asynchronously from the reader's point of view: a callback on delivery events lets the
// program wait for it, for a bounded time, before it reads.
var delivered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
using IDisposable watching = server.OnEvent(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    (e, cancellationToken) =>
    {
        delivered.TrySetResult(((StoreChangedEvent)e).Name);
        return ValueTask.CompletedTask;
    });

await using CurlClient client = CurlClient.Create();

// One attempt, on a client of its own because a client reuses a connection that is already logged in
// and would skip the login: returns "ok" and the body, or the curl error.
async Task<string> Try(CurlRequest request)
{
    await using CurlClient attempt = CurlClient.Create();
    try
    {
        CurlResponse response = await attempt.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        return "ok: " + (await new StreamReader(body, Encoding.ASCII).ReadToEndAsync()).Trim();
    }
    catch (CurlException failed)
    {
        return $"curl error {(int)failed.Code}: {failed.Message}";
    }
}

CurlAuthenticationOptions Login(string user, string password) => new() { UserName = user, Password = password };

// A send over SMTP as the given account; the mail itself is of no interest.
CurlRequest Send(string user, string password, string to) => new(HttpMethod.Put, smtp + "example.com")
{
    Authentication = Login(user, password),
    Mail = new CurlMailOptions { MailFrom = user, MailRecipients = [to] },
    Body = CurlContent.FromStream(new MemoryStream("Subject: x\r\n\r\nx\r\n"u8.ToArray())),
};

if (mode == "wrong")
{
    Console.WriteLine($"viewer, wrong password, IMAP: {await Try(new CurlRequest(HttpMethod.Get, imap) { Authentication = Login("viewer@example.com", "wrong") })}");
    return 0;
}

// app sends the viewer one message over SMTP, then the program waits up to ten seconds for it to land.
var send = new CurlRequest(HttpMethod.Put, smtp + "example.com")
{
    Authentication = Login("app@example.com", "apppass"),
    Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["viewer@example.com"] },
    Body = CurlContent.FromStream(new MemoryStream("From: app@example.com\r\nTo: viewer@example.com\r\nSubject: Hello\r\n\r\nA test message.\r\n"u8.ToArray())),
};
CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
await sent.Body.DisposeAsync();
Console.WriteLine($"app sent over SMTP: {sent.StatusCode}");
bool arrived = await Task.WhenAny(delivered.Task, Task.Delay(TimeSpan.FromSeconds(10))) == delivered.Task;
Console.WriteLine($"delivered: {(arrived ? await delivered.Task : "no delivery event within 10 s")}");

// The viewer on each protocol, with the right password.
Console.WriteLine($"viewer over IMAP: {await Try(new CurlRequest(HttpMethod.Get, imap + "INBOX;UID=1;SECTION=TEXT") { Authentication = Login("viewer@example.com", "viewpass") })}");
Console.WriteLine($"viewer over SMTP: {await Try(Send("viewer@example.com", "viewpass", "app@example.com"))}");
Console.WriteLine($"viewer over POP3: {await Try(new CurlRequest(HttpMethod.Get, pop3) { Authentication = Login("viewer@example.com", "viewpass") })}");

// A wrong password for app, an account that may use SMTP, and the reply every SMTP refusal got on the wire.
Console.WriteLine($"app, wrong password, over SMTP: {await Try(Send("app@example.com", "wrong", "viewer@example.com"))}");

// The server records an exchange's last bytes and its end a moment after the client has gone. The app's
// client still holds the first SMTP exchange open; wait, for at most 10 s, until every later one has ended.
var later = server.FindExchanges(new SurlEventQuery { Scheme = "smtp" }).Skip(1).Select(x => x.ExchangeId).ToList();
for (DateTime deadline = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < deadline; await Task.Delay(100))
{
    var ended = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded }).Cast<ExchangeEndedEvent>().Select(e => e.ExchangeId).ToHashSet();
    if (later.All(x => ended.Contains(x))) break;
}

string[] replies = server.FindExchanges(new SurlEventQuery { Scheme = "smtp" })
    .Select(x => Encoding.ASCII.GetString(x.Sent).Split("\r\n").FirstOrDefault(line => line.StartsWith("535")) ?? "")
    .Where(line => line != "").ToArray();
Console.WriteLine($"SMTP refusals on the wire: {replies.Length}, all \"{replies.Distinct().Single()}\"");

// app on IMAP and POP3: logged in, nothing to list in its own maildrop.
Console.WriteLine($"app over IMAP: {(await Try(new CurlRequest(HttpMethod.Get, imap) { Authentication = Login("app@example.com", "apppass") })).Split(':')[0]}");
Console.WriteLine($"app over POP3: {(await Try(new CurlRequest(HttpMethod.Get, pop3) { Authentication = Login("app@example.com", "apppass") })).Split(':')[0]}");

// The server's side: how it judged each of the viewer's logins, and what its mailbox holds.
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>().Where(l => l.Account == "viewer@example.com"))
{
    Console.WriteLine($"login judged: {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

Console.WriteLine($"messages in the viewer's INBOX: {(await server.Mail.ListMailboxesAsync("viewer@example.com")).Single(m => m.Name == "INBOX").MessageCount}");
return 0;
