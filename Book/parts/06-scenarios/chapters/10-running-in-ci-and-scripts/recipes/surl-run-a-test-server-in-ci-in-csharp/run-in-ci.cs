// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A CI test run against an in-process server: start HTTP and SMTP on free ports, report the
// bound URLs, run the tests, stop the server, and exit with a code the job can act on.
// Run: dotnet run run-in-ci.cs [-- pass|wrong-password|port-taken]
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "pass";

// port-taken: something else already holds the HTTP port the run asks for.
using var holder = new TcpListener(IPAddress.Loopback, 0);
holder.Start();
string httpListenUrl = mode == "port-taken"
    ? $"http://127.0.0.1:{((IPEndPoint)holder.LocalEndpoint).Port}"
    : "http://127.0.0.1:0";

var options = new SurlServerOptions { ListenUrls = { httpListenUrl, "smtp://127.0.0.1:0" } };
options.Files["greeting.txt"] = "Welcome aboard.\n"u8.ToArray();
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:secret");

// A server that cannot start fails the run with surl's own exit code.
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"server did not start: {refused.ExitCode} ({(int)refused.ExitCode})");
    return (int)refused.ExitCode;
}

int result;
try
{
    // Report the bound URLs, in the order of ListenUrls, for the job's log.
    Console.WriteLine($"HTTP_URL={server.BoundUrls[0]}");
    Console.WriteLine($"SMTP_URL={server.BoundUrls[1]}");
    result = await RunTests(server.BoundUrls[0], server.BoundUrls[1], mode == "wrong-password" ? "wrong" : "secret");

    // What the server recorded: the messages in the mailbox the tests mailed. Delivery follows the
    // accepted send a moment later, so after a passing run read again until the message is in, for up to 10 s.
    SurlMessagePage inbox = await server.Mail.ListMessagesAsync("app@example.com", "INBOX");
    for (var waited = System.Diagnostics.Stopwatch.StartNew(); result == 0 && inbox.Messages.Count == 0 && waited.Elapsed.TotalSeconds < 10;)
    {
        await Task.Delay(100);
        inbox = await server.Mail.ListMessagesAsync("app@example.com", "INBOX");
    }

    Console.WriteLine($"server stored {inbox.Messages.Count} message(s)");
}
finally
{
    // Stop the server whatever happened, and fail the run if it did not stop cleanly.
    SurlExitCode stopped = await server.StopAsync();
    Console.WriteLine($"server stopped: {stopped} ({(int)stopped})");
    if (stopped != SurlExitCode.Ok)
    {
        result = (int)stopped;
    }
}

return result;

// The tests: fetch the greeting over HTTP, mail it over SMTP. Returns 0, or curl's exit code.
static async Task<int> RunTests(Uri httpUrl, Uri smtpUrl, string password)
{
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse page = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{httpUrl}greeting.txt"), CancellationToken.None);
        string greeting;
        await using (Stream body = page.Body)
        {
            greeting = (await new StreamReader(body).ReadToEndAsync()).TrimEnd();
        }

        var mail = new CurlRequest(HttpMethod.Put, $"{smtpUrl}example.com")
        {
            Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = password },
            Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["app@example.com"] },
            Body = CurlContent.FromStream(new MemoryStream(Encoding.UTF8.GetBytes($"Subject: Greeting\r\n\r\n{greeting}\r\n"))),
        };
        CurlResponse sent = await client.SendAsync(mail, CancellationToken.None);
        await sent.Body.DisposeAsync();
        Console.WriteLine($"mailed: {greeting} (SMTP {sent.StatusCode})");
        return 0;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"test failed: {(int)failure.Code} {failure.Code}");
        return (int)failure.Code;
    }
}
