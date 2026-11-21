// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A class fixture for mail tests: MailFixture starts one in-process Surl server for every test,
// empties its store before each, and asserts on exactly the mail in it. The top of the file runs the
// two tests as an xUnit runner would: the fixture once, then each test after a reset.
// Run: dotnet run mail-fixture.cs [-- <case>]
//   (no argument)  the app sends one welcome mail per sign-up; both tests pass
//   sends-twice    the app has a bug and sends each welcome mail twice
//   no-reset       the runner skips the reset between tests
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "right";

// The runner: one fixture for the class, a reset before each test, a line per result.
await using MailFixture fixture = await MailFixture.StartAsync();
var tests = new SignupMailTests(fixture, new SignupMailer(fixture.Smtp, copies: mode == "sends-twice" ? 2 : 1));
(string Name, Func<Task> Run)[] all =
[
    (nameof(SignupMailTests.Signup_sends_one_welcome_mail), tests.Signup_sends_one_welcome_mail),
    (nameof(SignupMailTests.Second_signup_sees_only_its_own_mail), tests.Second_signup_sees_only_its_own_mail),
];
int failed = 0;
foreach ((string name, Func<Task> run) in all)
{
    if (mode != "no-reset")
    {
        await fixture.ResetAsync();
    }

    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (MailAssertException failure)
    {
        Console.WriteLine($"FAIL {name}: {failure.Message}");
        failed++;
    }
}

return failed == 0 ? 0 : 1;

// The tests: each signs someone up and asserts on every mail the server holds.
sealed class SignupMailTests(MailFixture fixture, SignupMailer app)
{
    public async Task Signup_sends_one_welcome_mail()
    {
        await app.SignUpAsync("ann@example.com");
        await fixture.AssertMailAsync("ann@example.com: Welcome to Example");
    }

    public async Task Second_signup_sees_only_its_own_mail()
    {
        await app.SignUpAsync("bob@example.com");
        await fixture.AssertMailAsync("bob@example.com: Welcome to Example");
    }
}

// The fixture: one server for the whole class; a fresh, empty store for each test.
sealed class MailFixture : IAsyncDisposable
{
    private static readonly string[] Owners = ["app@example.com", "ann@example.com", "bob@example.com"];

    private readonly SurlServer server;

    private MailFixture(SurlServer server) => this.server = server;

    public Uri Smtp => server.BoundUrl;

    public static async Task<MailFixture> StartAsync()
    {
        var options = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" } };
        options.MailDomains.Add("example.com");
        options.Mailboxes.Add("app@example.com:apppass");
        options.Mailboxes.Add("ann@example.com:annpass");
        options.Mailboxes.Add("bob@example.com:bobpass");
        return new MailFixture(await SurlServer.StartAsync(options));
    }

    // Mail, files and accounts back to how they were at start: every mailbox empty.
    public Task ResetAsync() => server.ResetAsync();

    // Every message in every mailbox, as "owner: subject", must be exactly the ones given.
    public async Task AssertMailAsync(params string[] want)
    {
        var got = new List<string>();
        foreach (string owner in Owners)
        {
            SurlMessagePage page = await server.Mail.ListMessagesAsync(owner, "INBOX");
            foreach (SurlMessageSummary summary in page.Messages)
            {
                SurlMailMessage message = await server.Mail.ReadMessageAsync(owner, "INBOX", summary.Uid);
                string subject = Encoding.ASCII.GetString(message.Bytes.Span).Split("\r\n").First(line => line.StartsWith("Subject: "))["Subject: ".Length..];
                got.Add($"{owner}: {subject}");
            }
        }

        if (!got.SequenceEqual(want))
        {
            throw new MailAssertException($"the server holds [{string.Join("; ", got)}], want [{string.Join("; ", want)}]");
        }
    }

    public ValueTask DisposeAsync() => server.DisposeAsync();
}

sealed class MailAssertException(string message) : Exception(message);

// The app under test: it sends a welcome mail on sign-up, over SMTP with Curl.Api.
sealed class SignupMailer(Uri smtp, int copies)
{
    public async Task SignUpAsync(string address)
    {
        await using CurlClient client = CurlClient.Create();
        for (int i = 0; i < copies; i++)
        {
            var send = new CurlRequest(HttpMethod.Put, $"{smtp}example.com")
            {
                Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "apppass" },
                Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [address] },
                Body = CurlContent.FromStream(new MemoryStream(Encoding.ASCII.GetBytes($"From: app@example.com\r\nTo: {address}\r\nSubject: Welcome to Example\r\n\r\nThanks for signing up.\r\n"))),
            };
            CurlResponse sent = await client.SendAsync(send, CancellationToken.None);
            await sent.Body.DisposeAsync();
        }
    }
}
