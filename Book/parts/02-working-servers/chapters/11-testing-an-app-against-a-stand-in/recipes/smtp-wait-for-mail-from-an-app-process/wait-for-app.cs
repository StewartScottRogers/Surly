// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app that runs as its own process: the test hosts an SMTP server in-process, starts
// the app with the server's URL, and awaits the app's mail with WaitForEventAsync, failing fast if
// the app exits with an error first. The app here is curl, sending order.eml as a mail app would.
// Run: dotnet run wait-for-app.cs [-- <case>]
//   (no argument)  the app sends the notice to Ann; the test waits for it
//   astray         the app's settings name another recipient; the wait times out
//   bad-login      the app's password is wrong; the app exits first
//   late           the test waits only after the app has exited
//   no-record      the server was started without RecordExchanges
#:package Surly.Surl.Api@0.1.13

using System.Diagnostics;
using System.Text;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "test";

// SMTP on a free port for example.com, keeping events so the test can wait on them.
var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" }, RecordExchanges = mode != "no-record" };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("test@example.com:testpass");
await using SurlServer server = await SurlServer.StartAsync(options);

// The app, a process of its own, told where the server is as its settings would tell it.
string recipient = mode == "astray" ? "test@example.com" : "ann@example.com";
string password = mode == "bad-login" ? "wrong" : "apppass";
using Process app = Process.Start(new ProcessStartInfo("curl")
{
    ArgumentList = { "-sS", "--crlf", "-u", $"app@example.com:{password}", "--mail-from", "app@example.com", "--mail-rcpt", recipient, "-T", "order.eml", $"{server.BoundUrl}shop.example.com" },
    RedirectStandardError = true,
})!;
Task<string> appErrors = app.StandardError.ReadToEndAsync();
var delivered = new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "ann@example.com" };

if (mode == "late")
{
    await app.WaitForExitAsync();
    SurlEvent kept = await server.WaitForEventAsync(delivered, TimeSpan.FromSeconds(10), CancellationToken.None);
    Console.WriteLine($"app exited {app.ExitCode}; then the wait answered {((StoreChangedEvent)kept).Name}");
    return 0;
}

// The test: the mail, or the app's exit, whichever comes first; at most 10 seconds for the mail.
// Without RecordExchanges the server keeps no events, and the call throws at once.
Task<SurlEvent> mail;
try
{
    mail = server.WaitForEventAsync(delivered, TimeSpan.FromSeconds(10), CancellationToken.None);
}
catch (InvalidOperationException refused)
{
    Console.WriteLine($"cannot wait: {refused.Message}");
    await app.WaitForExitAsync();
    return 1;
}

Task exited = app.WaitForExitAsync();
if (await Task.WhenAny(mail, exited) == exited && app.ExitCode != 0)
{
    Console.WriteLine($"FAIL: the app exited {app.ExitCode} before any mail: {(await appErrors).Trim()}");
    return 1;
}

try
{
    var arrived = (StoreChangedEvent)await mail;
    Console.WriteLine($"{arrived.Change} {arrived.Name}");
    SurlMailMessage copy = await server.Mail.ReadMessageAsync("ann@example.com", "INBOX", 1);
    Console.WriteLine(Encoding.ASCII.GetString(copy.Bytes.Span).Split("\r\n").First(line => line.StartsWith("Subject: ")));
}
catch (TimeoutException)
{
    Console.WriteLine("FAIL: no mail for ann@example.com within 10 s");
    await exited;
    Console.WriteLine($"the app exited {app.ExitCode}");
    return 1;
}

await exited;
Console.WriteLine($"PASS; the app exited {app.ExitCode}");
return 0;
