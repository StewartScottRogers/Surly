// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A mail poller that reads headers first: Curl.Api sends POP3 UIDL and TOP to an in-process Surl
// server, parses each Subject, and retrieves in full only the message it wants.
// Run: dotnet run peek-headers.cs [-- <case>]
//   (no argument)  UIDL, TOP n 0 for each message, then the one about a delay in full
//   lines          TOP 2 2: the header and the first two body lines
//   whole          TOP 2 99 against RETR 2
//   after-delete   DELE 1, then UIDL in the same session and in a new one
//   missing        TOP for a message that is not there
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "scan";

// POP3 on a free port, example.com, one mailbox holding three orders.
var options = new SurlServerOptions { ListenUrls = { "pop3://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
string pop3 = $"{server.BoundUrls[0]}";

string[] subjects = ["Order 1 shipped", "Order 2 delayed", "Order 3 shipped"];
foreach (string subject in subjects)
{
    await server.Mail.DeliverAsync("ann@example.com", Encoding.ASCII.GetBytes(
        $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nFirst line.\r\nSecond line.\r\nThird line.\r\n"));
}

await using CurlClient client = CurlClient.Create();
var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };

// A POP3 command is the request's method; curl appends the URL path to it, so "TOP" with the path
// "2%200" sends "TOP 2 0". The answer is the response body.
async Task<string> Pop3(string command, string path, CurlClient? via = null)
{
    CurlResponse response = await (via ?? client).SendAsync(new CurlRequest(new HttpMethod(command), pop3 + path) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body, Encoding.ASCII).ReadToEndAsync();
}

if (mode == "lines")
{
    Console.Write((await Pop3("TOP", "2%202")).Split("\r\n\r\n")[1]);
    return 0;
}

if (mode == "whole")
{
    // More lines than the body has: TOP answers the whole message, byte for byte what RETR answers.
    string top = await Pop3("TOP", "2%2099");
    string retr = await Pop3("RETR", "2");
    Console.WriteLine($"TOP 2 99: {top.Length} bytes; RETR 2: {retr.Length} bytes; same: {top == retr}");
    return 0;
}

if (mode == "after-delete")
{
    // DELE in this client's session; the session lists 2 and 3 under their old numbers until it ends.
    await Pop3("DELE", "1");
    Console.Write($"same session:\n{(await Pop3("UIDL", "")).Replace("\r\n", "\n")}");

    // A new client is a new session; disposing the first sends QUIT, which makes the deletion take effect.
    await client.DisposeAsync();

    // The server applies the deletion when it handles that QUIT, a moment after the client is gone: wait,
    // for up to 10 s, until the store no longer holds message 1.
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    while ((await server.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages.Any(m => m.Uid == 1) && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
    }

    await using CurlClient next = CurlClient.Create();
    Console.Write($"next session:\n{(await Pop3("UIDL", "", next)).Replace("\r\n", "\n")}");
    return 0;
}

if (mode == "missing")
{
    try
    {
        await Pop3("TOP", "9%200");
        return 0;
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"curl error {(int)failed.Code}: {failed.Message}");
        return 1;
    }
}

// UIDL: one line per message, its number and its unique ID.
string[] ids = (await Pop3("UIDL", "")).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
int wanted = 0;
foreach (string line in ids)
{
    string number = line.Split(' ')[0];
    string header = await Pop3("TOP", $"{number}%200");
    string subject = header.Split("\r\n").Single(h => h.StartsWith("Subject: ")).Substring("Subject: ".Length);
    Console.WriteLine($"{line}: {subject} ({header.Length} header bytes)");
    if (subject.Contains("delayed"))
    {
        wanted = int.Parse(number);
    }
}

// Only the delayed order is retrieved in full: a path with a number alone is RETR.
string full = await Pop3("RETR", $"{wanted}");
SurlMessagePage page = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX");
long size = page.Messages.Single(m => m.Uid == wanted).Size;
Console.WriteLine($"retrieved message {wanted}: {full.Length} bytes, the store says {size}");
return 0;
