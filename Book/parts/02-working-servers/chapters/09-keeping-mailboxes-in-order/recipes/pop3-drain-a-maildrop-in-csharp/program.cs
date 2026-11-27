// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A mail poller that drains a maildrop: Curl.Api lists an in-process Surl server's POP3 maildrop, retrieves every
// message into a file, deletes each, and the test checks that the store and a new session find it empty.
// Run: dotnet run program.cs [-- case]
//   (no argument)  drain three messages: retrieve each into a file, then DELE each
//   whole          the files hold the three orders, byte for byte what the store holds
//   next-session   a new session lists nothing
//   count          the store's message count before and after
//   empty          retrieve message 1 of the drained maildrop
//   listing        the listing before the drain: three lines, sizes the store gives
//   unwritable     the folder for the files does not exist: the save fails before any DELE
//   unwritable-kept  the same, and the store after it
//   rset           DELE each, then RSET, then the end of the session
//   rset-kept      the same, and a new session's listing
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "drain";

// POP3 on a free port for example.com, one mailbox. RecordExchanges makes the server record the end of each session,
// which is what the test waits for, because a deletion takes effect when the session ends with QUIT.
var options = new SurlServerOptions { ListenUrls = { "pop3://127.0.0.1:0" }, RecordExchanges = true };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
string pop3 = $"{server.BoundUrls[0]}";
var login = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" };

// Three orders, put in as a delivery does; DeliverAsync returns once each is stored.
string[] subjects = ["Order 1 shipped", "Order 2 shipped", "Order 3 shipped"];
foreach (string subject in subjects)
{
    await server.Mail.DeliverAsync("ann@example.com", Encoding.ASCII.GetBytes(Order(subject)));
}

DirectoryInfo files = Directory.CreateTempSubdirectory("surl-drain-");
long lastSequence = 0;

try
{
    switch (which)
    {
        case "listing":
            await using (CurlClient client = CurlClient.Create())
            {
                string[] lines = await Listing(client);
                SurlMessagePage page = await server.Mail.ListMessagesAsync("ann@example.com", "INBOX");
                bool same = lines.Select(l => long.Parse(l.Split(' ')[1])).SequenceEqual(page.Messages.Select(m => m.Size));
                Console.WriteLine($"listing: {lines.Length} messages, octets match the store: {same}");
            }

            return 0;
        case "unwritable":
        case "unwritable-kept":
            try
            {
                await using CurlClient client = CurlClient.Create();
                await Drain(client, Path.Combine(files.FullName, "nosuch"));
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("save failed: no DELE sent");
            }

            // The client is disposed, which ends its session; the server has recorded the end before the store is read.
            await EndOfSession();
            if (which == "unwritable-kept")
            {
                Console.WriteLine($"in the store: {await InStore()}");
            }

            return 0;
        case "rset":
        case "rset-kept":
            await using (CurlClient client = CurlClient.Create())
            {
                // DELE marks; RSET unmarks every message marked in the session.
                foreach (int number in Enumerable.Range(1, subjects.Length))
                {
                    await Pop3(client, "DELE", $"{number}");
                }

                await Pop3(client, "RSET", "");
                Console.WriteLine("DELE 1, DELE 2, DELE 3, RSET sent");
            }

            await EndOfSession();
            if (which == "rset-kept")
            {
                await using CurlClient next = CurlClient.Create();
                Console.WriteLine($"next session lists {(await Listing(next)).Length} messages");
            }

            return 0;
    }

    // The drain: every message is retrieved into a file first, and only then deleted.
    int retrieved, deleted;
    await using (CurlClient client = CurlClient.Create())
    {
        (retrieved, deleted) = await Drain(client, files.FullName);
    }

    // Disposing the client sent QUIT, which is what commits the deletions; wait for the server to record the end of
    // that session, with a bound, before reading anything it changed.
    await EndOfSession();

    switch (which)
    {
        case "whole":
            // The files were written before the deletion, from what RETR answered.
            foreach (int number in Enumerable.Range(1, subjects.Length))
            {
                byte[] saved = await File.ReadAllBytesAsync(Path.Combine(files.FullName, $"msg{number}.eml"));
                string text = Encoding.ASCII.GetString(saved);
                Console.WriteLine($"msg{number}.eml: {text.Split("\r\n").First(h => h.StartsWith("Subject: "))}, same as delivered: {text == Order(subjects[number - 1])}");
            }

            return 0;
        case "next-session":
            await using (CurlClient next = CurlClient.Create())
            {
                Console.WriteLine($"next session lists {(await Listing(next)).Length} messages");
            }

            return 0;
        case "count":
            Console.WriteLine($"in the store after the drain: {await InStore()}");
            return 0;
        case "empty":
            try
            {
                await using CurlClient again = CurlClient.Create();
                await Pop3(again, "RETR", "1");
                return 0;
            }
            catch (CurlException failed)
            {
                Console.WriteLine($"curl error {(int)failed.Code}: {failed.Message}");
                return (int)failed.Code;
            }
        default:
            Console.WriteLine($"retrieved {retrieved}, deleted {deleted}, files {files.GetFiles("msg*.eml").Length}");
            return 0;
    }
}
finally
{
    files.Delete(true);
}

// One order as a message: the lines end in carriage return and line feed, as a message's do.
static string Order(string subject) =>
    $"From: shop@example.com\r\nTo: ann@example.com\r\nSubject: {subject}\r\n\r\nYour order is on its way.\r\n";

// A POP3 command is the request's method; curl appends the URL's path to it. The answer is the response body.
async Task<byte[]> Pop3(CurlClient client, string command, string path)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(new HttpMethod(command), pop3 + path) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    using var bytes = new MemoryStream();
    await body.CopyToAsync(bytes);
    return bytes.ToArray();
}

// LIST: one line a message, its number and its size in octets.
async Task<string[]> Listing(CurlClient client)
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, pop3) { Authentication = login }, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body, Encoding.ASCII).ReadToEndAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}

// The drain: list, retrieve each message into a file, and only when every file is written delete each message. A
// failure while saving throws before the first DELE, so nothing is deleted that was not saved.
async Task<(int Retrieved, int Deleted)> Drain(CurlClient client, string folder)
{
    int[] numbers = (await Listing(client)).Select(l => int.Parse(l.Split(' ')[0])).ToArray();
    foreach (int number in numbers)
    {
        byte[] message = await Pop3(client, "RETR", $"{number}");
        await File.WriteAllBytesAsync(Path.Combine(folder, $"msg{number}.eml"), message);
    }

    foreach (int number in numbers)
    {
        await Pop3(client, "DELE", $"{number}");
    }

    return (numbers.Length, numbers.Length);
}

// The end of the client's session, as the server records it: the exchange has ended, so QUIT has been handled.
async Task EndOfSession()
{
    SurlEvent ended = await server.WaitForEventAsync(
        new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded, AfterSequence = lastSequence }, TimeSpan.FromSeconds(10));
    lastSequence = ended.Sequence;
}

// What the store holds for Ann, read with no client.
async Task<int> InStore() => (await server.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages.Count;
