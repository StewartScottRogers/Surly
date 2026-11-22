// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A mail flow with an OAuth 2.0 token, end to end: an in-process Surl server that accepts only OAUTHBEARER and
// XOAUTH2, a send and a read with the token, what the server stored and judged, and every way in that stays shut.
// Run: dotnet run token-mail.cs [-- why]   (why: ordinary mailboxes, to show what protocols=http closes)
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

// smtps and imaps over TLS, smtp and imap without, over one mail store. The token is an account with an empty
// user name; the mailboxes take mail but log in on no mail protocol (protocols=http), so a password is no way in.
var options = new SurlServerOptions
{
    ListenUrls = { "smtps://127.0.0.1:0", "imaps://127.0.0.1:0", "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    SelfSigned = true,
    AuthenticationMethods = ["oauthbearer", "xoauth2"],
    RecordExchanges = true,
};
options.Accounts.Add(new SurlAccount("", "tok-abc123"));
options.MailDomains.Add("example.com");
// With the argument "why" the mailboxes are ordinary ones, to show what protocols=http closes.
string closed = args.Length > 0 && args[0] == "why" ? "" : ";protocols=http";
options.Mailboxes.Add("ann@example.com:annpass" + closed);
options.Mailboxes.Add("bob@example.com:bobpass" + closed);
await using var server = await SurlServer.StartAsync(options);
string smtps = $"{server.BoundUrls[0]}", imaps = $"{server.BoundUrls[1]}", smtp = $"{server.BoundUrls[2]}", imap = $"{server.BoundUrls[3]}";

// Deliveries are events. The program counts them as they come and waits for a number, for a bounded time.
var delivered = new List<string>();
var changed = new SemaphoreSlim(0);
using IDisposable watching = server.OnEvent(
    new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail },
    (e, cancellationToken) =>
    {
        lock (delivered)
        {
            delivered.Add(((StoreChangedEvent)e).Name);
        }

        changed.Release();
        return ValueTask.CompletedTask;
    });

var trustIt = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) } };
byte[] lunch = "From: bob@example.com\r\nTo: ann@example.com\r\nSubject: Lunch\r\n\r\nNoon at the usual place.\r\n"u8.ToArray();

// One request on a client of its own, because a client reuses a connection that is already logged in.
// Returns what the server answered, or the curl error.
async Task<string> Run(CurlRequest request)
{
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(request with { Tls = trustIt }, CancellationToken.None);
        await using Stream body = response.Body;
        string text = (await new StreamReader(body, Encoding.ASCII).ReadToEndAsync()).Trim();
        return request.Method == HttpMethod.Put ? $"SMTP {response.StatusCode}" : text;
    }
    catch (CurlException failed)
    {
        // A refused login is just its code. A refused MAIL or RCPT keeps the server's reply, which says which.
        return (int)failed.Code == 55 ? $"curl error 55: {failed.Message}" : $"curl error {(int)failed.Code}";
    }
}

CurlRequest Send(string url, string user, string? password, string? token, string to = "ann@example.com", string? mechanism = null) =>
    new(HttpMethod.Put, url + "example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = user, Password = password, XOAuth2Bearer = token, LoginOptions = mechanism },
        Mail = new CurlMailOptions { MailFrom = "bob@example.com", MailRecipients = [to] },
        Body = CurlContent.FromStream(new MemoryStream(lunch)),
    };

CurlRequest Read(string url, string user, string? password, string? token) =>
    new(HttpMethod.Get, url + "INBOX;UID=1;SECTION=TEXT")
    {
        Authentication = new CurlAuthenticationOptions { UserName = user, Password = password, XOAuth2Bearer = token },
    };

if (closed == "")
{
    Console.WriteLine($"ann's password over imaps, no protocols=http: {await Run(new CurlRequest(HttpMethod.Get, imaps) { Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" } })}");
    return 0;
}

// The happy path: bob sends with the token, the program waits up to ten seconds for the delivery, ann reads.
Console.WriteLine($"bob sends over smtps with the token: {await Run(Send(smtps, "bob@example.com", null, "tok-abc123"))}");
bool arrived = await changed.WaitAsync(TimeSpan.FromSeconds(10));
Console.WriteLine($"delivered: {(arrived ? delivered[0] : "no delivery event within 10 s")}");
Console.WriteLine($"ann reads over imaps with the token: {await Run(Read(imaps, "ann@example.com", null, "tok-abc123"))}");

// What the server stored: the message as it holds it, read straight from the store.
SurlMailMessage stored = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", 1);
string headers = Encoding.ASCII.GetString(stored.Bytes.Span);
Console.WriteLine($"stored with ESMTPSA, from bob: {headers.Contains("with ESMTPSA;") && headers.StartsWith("Return-Path: <bob@example.com>")}");

// XOAUTH2 on request, then every way in that stays shut.
Console.WriteLine($"bob sends with XOAUTH2: {await Run(Send(smtps, "bob@example.com", null, "tok-abc123", mechanism: "AUTH=XOAUTH2"))}");
await changed.WaitAsync(TimeSpan.FromSeconds(10));
Console.WriteLine($"bob's password over smtps: {await Run(Send(smtps, "bob@example.com", "bobpass", null))}");
Console.WriteLine($"ann's password over imaps: {await Run(Read(imaps, "ann@example.com", "annpass", null))}");
Console.WriteLine($"wrong token over smtps: {await Run(Send(smtps, "bob@example.com", null, "wrong-token"))}");
Console.WriteLine($"token over plain smtp: {await Run(Send(smtp, "bob@example.com", null, "tok-abc123"))}");
Console.WriteLine($"token over plain imap: {await Run(Read(imap, "ann@example.com", null, "tok-abc123"))}");
Console.WriteLine($"token to a recipient outside the domain: {await Run(Send(smtps, "bob@example.com", null, "tok-abc123", to: "carol@elsewhere.org"))}");

// What the server recorded: how many messages are in ann's INBOX, and every login it judged.
Console.WriteLine($"messages in ann's INBOX: {(await server.Mail.ListMailboxesAsync("ann@example.com")).Single(m => m.Name == "INBOX").MessageCount}");
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"login judged: {login.Scheme} {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

return 0;
