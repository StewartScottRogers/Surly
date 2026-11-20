// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of an app's sign-up mail: the app sends it with SmtpClient to an in-process Surl server,
// and the test reads it back over IMAP with Curl.Api and checks From, To and Subject.
// Run: dotnet run check-signup.cs [-- <case>]
//   (no argument)    the app sends the mail it should; the test passes
//   wrong-subject    the app misspells the subject
//   wrong-sender     the app sends from noreply@example.org
//   wrong-recipient  the app mails an@example.com instead of ann@example.com
//   no-plaintext     the server keeps its default, no password in clear without TLS
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Net;
using System.Net.Mail;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "right";

// The server: SMTP and IMAP on free ports over one mail store, serving example.com.
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    AllowPlaintextAuthentication = mode != "no-plaintext",
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
await using var server = await SurlServer.StartAsync(options);
Uri smtp = server.BoundUrls[0];
string imap = $"{server.BoundUrls[1]}";

// The app under test sends its sign-up mail, here with its mistakes chosen by the argument.
string from = mode == "wrong-sender" ? "noreply@example.org" : "app@example.com";
string to = mode == "wrong-recipient" ? "an@example.com" : "ann@example.com";
string subject = mode == "wrong-subject" ? "Welcome to Exmaple" : "Welcome to Example";
using (var app = new SmtpClient(smtp.Host, smtp.Port) { Credentials = new NetworkCredential("app@example.com", "apppass") })
using (var mail = new MailMessage(from, to, subject, "Thanks for signing up."))
{
    try
    {
        app.Send(mail);
    }
    catch (SmtpException refused)
    {
        Console.WriteLine($"app could not send: {refused.StatusCode}: {refused.Message}");
        return 1;
    }
}

// The test: Ann's first message, only its From, To and Subject fields, over IMAP.
string fields;
await using (CurlClient client = CurlClient.Create())
{
    CurlRequest fetch = new(HttpMethod.Get, $"{imap}INBOX;UID=1;SECTION=HEADER.FIELDS%20(FROM%20TO%20SUBJECT)")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" },
    };
    try
    {
        CurlResponse response = await client.SendAsync(fetch, CancellationToken.None);
        await using Stream body = response.Body;
        fields = await new StreamReader(body).ReadToEndAsync();
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"FAIL: no mail for ann@example.com (curl error {(int)failure.Code}: {failure.Message})");
        return 1;
    }
}

// Each field the test expects, compared with what arrived.
var got = fields.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
    .Select(line => line.Split(": ", 2)).ToDictionary(pair => pair[0], pair => pair[1]);
var want = new Dictionary<string, string>
{
    ["From"] = "app@example.com", ["To"] = "ann@example.com", ["Subject"] = "Welcome to Example",
};
int wrong = 0;
foreach ((string name, string value) in want)
{
    string actual = got.GetValueOrDefault(name, "(none)");
    Console.WriteLine(actual == value ? $"{name}: {actual}" : $"FAIL: {name} is {actual}, want {value}");
    wrong += actual == value ? 0 : 1;
}

return wrong == 0 ? 0 : 1;
