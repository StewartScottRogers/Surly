// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// One in-process Surl.Api server for a whole test class, as an xUnit class fixture holds it: started
// once, reset before each test, stopped after the last. A small runner stands in for the framework.
// Run: dotnet run class-fixture.cs [-- reverse|no-reset]
//   (no argument)  the three tests in order, each after a reset
//   reverse        the same tests in the opposite order
//   no-reset       in order, with no reset between tests
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "in-order";

// The runner: the fixture once for the class, a reset before each test, a line per result.
await using ServerFixture fixture = await ServerFixture.StartAsync();
var tests = new ShopTests(fixture);
(string Name, Func<Task> Run)[] all =
[
    (nameof(ShopTests.Upload_adds_one_report), tests.Upload_adds_one_report),
    (nameof(ShopTests.Signup_sends_one_mail), tests.Signup_sends_one_mail),
    (nameof(ShopTests.Starts_from_the_seeded_state), tests.Starts_from_the_seeded_state),
];
if (mode == "reverse")
{
    Array.Reverse(all);
}

var urls = new HashSet<string>();
int failed = 0;
foreach ((string name, Func<Task> run) in all)
{
    if (mode != "no-reset")
    {
        await fixture.ResetAsync();
    }

    urls.Add($"{fixture.Http} {fixture.Smtp}");
    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception failure)
    {
        Console.WriteLine($"FAIL {name}: {failure.Message}");
        failed++;
    }
}

Console.WriteLine($"server addresses the tests saw: {urls.Count}");
return failed == 0 ? 0 : 1;

// The test class: each test changes the server, then asserts on everything it holds.
sealed class ShopTests(ServerFixture fixture)
{
    public async Task Upload_adds_one_report()
    {
        await fixture.App.UploadReportAsync("report.csv", "id,total\n1,40\n");
        await fixture.AssertAsync(files: "config.json, report.csv", mail: 0);
    }

    public async Task Signup_sends_one_mail()
    {
        await fixture.App.SignUpAsync("ann@example.com");
        await fixture.AssertAsync(files: "config.json", mail: 1);
    }

    public async Task Starts_from_the_seeded_state() =>
        await fixture.AssertAsync(files: "config.json", mail: 0);
}

// The fixture: one server for the class. ResetAsync puts back what StartAsync gave it.
sealed class ServerFixture : IAsyncDisposable
{
    private readonly SurlServer server;

    private ServerFixture(SurlServer server)
    {
        this.server = server;
        App = new ShopApp(server.BoundUrls[0], server.BoundUrls[1]);
    }

    public Uri Http => server.BoundUrls[0];

    public Uri Smtp => server.BoundUrls[1];

    public ShopApp App { get; }

    public static async Task<ServerFixture> StartAsync()
    {
        var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0", "smtp://127.0.0.1:0" }, AllowUploads = true };
        options.Files["config.json"] = "{\"shop\":\"open\"}\n"u8.ToArray();
        options.MailDomains.Add("example.com");
        options.Mailboxes.Add("app@example.com:apppass;protocols=smtp,http");
        options.Mailboxes.Add("ann@example.com:annpass");
        return new ServerFixture(await SurlServer.StartAsync(options));
    }

    // Files back to config.json alone, every mailbox empty, accounts as at start.
    public Task ResetAsync() => server.ResetAsync();

    // The served files, by name, and the number of messages in ann's inbox, must be as given.
    public async Task AssertAsync(string files, int mail)
    {
        string gotFiles = string.Join(", ", (await server.Files.ListAsync("/")).Entries.Select(e => e.Name));
        int gotMail = (await server.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages.Count;
        if (gotFiles != files || gotMail != mail)
        {
            throw new Exception($"server holds files [{gotFiles}] and {gotMail} mail, want [{files}] and {mail}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await server.DisposeAsync();
    }
}

// The app under test: it uploads reports over HTTP and mails new customers over SMTP.
sealed class ShopApp(Uri http, Uri smtp) : IAsyncDisposable
{
    private readonly CurlClient client = CurlClient.Create();
    private readonly CurlAuthenticationOptions login = new() { UserName = "app@example.com", Password = "apppass" };

    public Task UploadReportAsync(string name, string text) => Send(new CurlRequest(HttpMethod.Put, $"{http}{name}")
    {
        Authentication = login with { HttpAuthentication = HttpAuthenticationMethods.Digest },
        Body = Encoding.UTF8.GetBytes(text),
    });

    public Task SignUpAsync(string address) => Send(new CurlRequest(HttpMethod.Put, $"{smtp}example.com")
    {
        Authentication = login,
        Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = [address] },
        Body = CurlContent.FromStream(new MemoryStream(Encoding.UTF8.GetBytes($"From: app@example.com\r\nTo: {address}\r\nSubject: Welcome\r\n\r\nThanks for signing up.\r\n"))),
    });

    private async Task Send(CurlRequest request)
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await response.Body.DisposeAsync();
    }

    public ValueTask DisposeAsync() => client.DisposeAsync();
}
