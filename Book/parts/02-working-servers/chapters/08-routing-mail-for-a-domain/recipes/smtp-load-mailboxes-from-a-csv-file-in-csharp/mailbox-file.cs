// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test fixture that writes a fresh mailbox CSV for each run, starts surl in-process with it, and
// catches the start refusal that names a bad record's line.
// Run: dotnet run mailbox-file.cs [-- outside | twice | column | missing]
//   (no argument)  forty testers and ops: load them, send, read, log in over each protocol
//   outside        a record outside every mail domain, after a blank line
//   twice          a second file that repeats tester05
//   column         a header naming a column surl does not know
//   missing        a file that is not there
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "load";

// This run's own folder, so parallel runs never share a file.
DirectoryInfo run = Directory.CreateTempSubdirectory("mailboxes-");
try
{
    // Forty testers; the last ten log in over IMAP only. ops has a comma in its password, so it is quoted.
    var csv = new StringBuilder("address,password,protocols\n");
    for (int i = 1; i <= 40; i++)
    {
        csv.Append($"tester{i:00}@example.com,pass{i:00},{(i <= 30 ? "" : "imap")}\n");
    }

    csv.Append("ops@example.com,\"s3cret,too\",imap\n");
    string mailboxes = Path.Combine(run.FullName, "mailboxes.csv");
    string other = Path.Combine(run.FullName, "other.csv");
    File.WriteAllText(mailboxes, mode switch
    {
        "outside" => "address,password,protocols\nops@example.com,\"s3cret,too\",imap\n\ncarol@elsewhere.org,carolpass,\n",
        "column" => "address,pass\nann@example.com,annpass\n",
        _ => csv.ToString(),
    });
    File.WriteAllText(other, "address,password\ntester05@example.com,another\n");

    // The server writes its log from its own threads; every read takes the same lock its writes do.
    var log = new StringWriter();
    TextWriter shared = TextWriter.Synchronized(log);
    var options = new SurlServerOptions
    {
        ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "pop3://127.0.0.1:0" },
        RecordExchanges = true,
        Log = shared,
        LogLevel = LogLevel.Verbose,
    };
    options.MailDomains.Add("example.com");
    options.MailboxFiles.Add(mode == "missing" ? Path.Combine(run.FullName, "missing.csv") : mailboxes);
    if (mode == "twice")
    {
        options.MailboxFiles.Add(other);
    }

    SurlServer server;
    try
    {
        server = await SurlServer.StartAsync(options);
    }
    catch (SurlServerStartException refused)
    {
        // The line names the file by its full path; print it by name so the output reads the same everywhere.
        Console.WriteLine($"{refused.GetType().Name}: {refused.ExitCode}, {refused.Lines[0].Replace(run.FullName + Path.DirectorySeparatorChar, "")}");
        return 1;
    }

    await using (server)
    {
        string smtp = $"{server.BoundUrls[0]}", imap = $"{server.BoundUrls[1]}", pop3 = $"{server.BoundUrls[2]}";

        // tester01 mails tester02; the test waits for the delivery, then reads the subject over IMAP.
        await using (CurlClient client = CurlClient.Create())
        {
            await client.SendAsync(new CurlRequest(HttpMethod.Put, smtp + "ci.example.com")
            {
                Mail = new CurlMailOptions { MailFrom = "tester01@example.com", MailRecipients = ["tester02@example.com"] },
                Authentication = Login("tester01@example.com", "pass01"),
                Body = "From: tester01@example.com\r\nTo: tester02@example.com\r\nSubject: Build 812 passed\r\n\r\nAll 40 suites passed.\r\n"u8.ToArray(),
            }, CancellationToken.None);
        }

        await server.WaitForEventAsync(
            new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "tester02@example.com" }, TimeSpan.FromSeconds(10));
        Console.WriteLine($"tester02 over IMAP: {await Read(imap + "INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)", "tester02@example.com", "pass02")}");

        // What the log named at start: one line per mailbox, tester31's with its protocols.
        string[] started = [];
        for (int tries = 0; tries < 100 && !started.Any(l => l.StartsWith("* Mailbox tester31@example.com:")); tries++)
        {
            if (tries > 0)
            {
                await Task.Delay(100);
            }

            lock (shared)
            {
                started = log.ToString().Split('\n').Select(l => l.TrimEnd()).ToArray();
            }
        }

        Console.WriteLine($"testers in the log: {started.Count(l => l.StartsWith("* Mailbox tester"))}");
        Console.WriteLine($"log: {started.Single(l => l.StartsWith("* Mailbox tester31@example.com:"))}");

        Console.WriteLine($"tester31 over POP3: {await Read(pop3, "tester31@example.com", "pass31")}");
        Console.WriteLine($"tester31 over IMAP: {await Read(imap, "tester31@example.com", "pass31", "STATUS INBOX (MESSAGES)")}");
        Console.WriteLine($"tester02 over POP3: {await Read(pop3, "tester02@example.com", "pass02")}");
        Console.WriteLine($"ops over IMAP: {await Read(imap, "ops@example.com", "s3cret,too", "STATUS INBOX (MESSAGES)")}");
    }

    return 0;
}
finally
{
    run.Delete(recursive: true);
}

static CurlAuthenticationOptions Login(string user, string password) => new() { UserName = user, Password = password };

// One read on a fresh client: the body, or the CurlException's code.
static async Task<string> Read(string url, string user, string password, string? command = null)
{
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Get, url) { Authentication = Login(user, password), CustomRequest = command }, CancellationToken.None);
        await using Stream body = response.Body;
        return (await new StreamReader(body).ReadToEndAsync()).Trim();
    }
    catch (CurlException refused)
    {
        return $"CurlException {refused.Code} ({(int)refused.Code}): {refused.Message}";
    }
}
