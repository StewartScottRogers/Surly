// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A mail client logs in to an in-process Surl SMTP server and sends one mail with Curl.Api; the
// program then reads back, from the server, which mechanism was judged and what crossed the wire.
// Run: dotnet run log-in.cs [-- <case>]
//   (no argument)  curl's default choice, CRAM-MD5, with the right password
//   wrong          CRAM-MD5 with a wrong password
//   plain          the client insists on PLAIN (LoginOptions AUTH=PLAIN)
//   raw            a client that sends AUTH PLAIN and AUTH LOGIN whatever the server offers
//   loose          AUTH=PLAIN against a server with AllowPlaintextAuthentication
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Net.Sockets;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "cram";

// SMTP for example.com, two mailboxes; the server records every exchange so the test can read it.
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0" },
    AllowPlaintextAuthentication = mode == "loose",
    RecordExchanges = true,
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("bob@example.com:bobpass");
await using var server = await SurlServer.StartAsync(options);
Uri smtp = server.BoundUrl;

if (mode == "raw")
{
    // EHLO, PLAIN with the right password, PLAIN with a wrong one, LOGIN, QUIT; then every reply.
    using var tcp = new TcpClient(smtp.Host, smtp.Port);
    using NetworkStream wire = tcp.GetStream();
    await wire.WriteAsync("EHLO client\r\nAUTH PLAIN AGFubkBleGFtcGxlLmNvbQBhbm5wYXNz\r\nAUTH PLAIN AGFubkBleGFtcGxlLmNvbQB3cm9uZw==\r\nAUTH LOGIN\r\nQUIT\r\n"u8.ToArray());
    string replies = await new StreamReader(wire, Encoding.ASCII).ReadToEndAsync();
    foreach (string line in replies.Split("\r\n").Where(l => l.StartsWith("250 ") || l.StartsWith("235") || l.StartsWith("538")))
    {
        Console.WriteLine(line);
    }

    return 0;
}

// The client: Curl.Api sends SMTP with a stream body. Without LoginOptions it picks the
// strongest mechanism the server offers.
var mail = new CurlRequest(HttpMethod.Put, $"{smtp}example.com")
{
    Authentication = new CurlAuthenticationOptions
    {
        UserName = "ann@example.com",
        Password = mode == "wrong" ? "wrong" : "annpass",
        LoginOptions = mode is "plain" or "loose" ? "AUTH=PLAIN" : null,
    },
    Mail = new CurlMailOptions { MailFrom = "ann@example.com", MailRecipients = ["bob@example.com"] },
    Body = CurlContent.FromStream(new MemoryStream("Subject: Lunch\r\n\r\nNoon at the usual place.\r\n"u8.ToArray())),
};
int exit = 0;
await using (CurlClient client = CurlClient.Create())
{
    try
    {
        CurlResponse sent = await client.SendAsync(mail, CancellationToken.None);
        await sent.Body.DisposeAsync();
        Console.WriteLine($"sent: SMTP {sent.StatusCode}");
    }
    catch (CurlException failed)
    {
        Console.WriteLine($"not sent: curl error {(int)failed.Code}: {failed.Message}");
        exit = 1;
    }
}

// What the server judged, and what the client put on the wire.
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"login judged: {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

string received = string.Concat(server.FindExchanges(new SurlEventQuery { Scheme = "smtp" }).Select(x => Encoding.ASCII.GetString(x.Received)));
string auth = received.Split("\r\n").FirstOrDefault(line => line.StartsWith("AUTH ")) ?? "(no AUTH)";
string plain = Convert.ToBase64String("\0ann@example.com\0annpass"u8);
Console.WriteLine($"client sent: {auth}");
Console.WriteLine($"password on the wire, as is or in base64: {received.Contains("annpass") || received.Contains(plain)}");
return exit;
