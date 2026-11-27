// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An ASP.NET Core app is started in the test's own process beside a Surl server, and the server's bound
// addresses are put into the app's configuration before it starts. Run: dotnet run program.cs [-- case]
//   (no argument)  the app reads rates over HTTP and mails a sign-up, both through the server
//   two-apps       two apps started from the same factory, each on its own port, one server
//   no-injection   the app is started without the addresses, so it points where the default says
//   no-plaintext   the server keeps its default and refuses the app's mail password
#:sdk Microsoft.NET.Sdk.Web
#:package Surly.Surl.Api@0.1.12

using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "beside";

// The server the app depends on: its rates over HTTP, its mail over SMTP. Port 0 takes a free port for each.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "smtp://127.0.0.1:0" },
    AllowPlaintextAuthentication = which != "no-plaintext",
    RecordExchanges = true,
};
options.Files["rates.json"] = "{\"eur\":1.08}"u8.ToArray();
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
await using var surl = await SurlServer.StartAsync(options);

// The factory: what the app needs from the server, written as configuration, as a test host would.
var settings = new Dictionary<string, string?>();
if (which != "no-injection")
{
    settings["Rates:Url"] = surl.BoundUrls[0].ToString();
    settings["Smtp:Host"] = surl.BoundUrls[1].Host;
    settings["Smtp:Port"] = surl.BoundUrls[1].Port.ToString();
}

await using App app = await App.StartAsync(settings);
using var client = new HttpClient { BaseAddress = app.Address };

if (which == "two-apps")
{
    await using App second = await App.StartAsync(settings);
    using var other = new HttpClient { BaseAddress = second.Address };
    Console.WriteLine($"two ports: {app.Address.Port != second.Address.Port}");
    Console.WriteLine($"first app /rates: {(int)(await client.GetAsync("/rates")).StatusCode}");
    Console.WriteLine($"second app /rates: {(int)(await other.GetAsync("/rates")).StatusCode}");
    return 0;
}

Console.WriteLine($"app and server on different ports: {app.Address.Port != surl.BoundUrls[0].Port}");
HttpResponseMessage rates = await client.GetAsync("/rates");
Console.WriteLine($"/rates: {(int)rates.StatusCode} {await rates.Content.ReadAsStringAsync()}");
if (which == "no-injection")
{
    return 1;
}

HttpResponseMessage signup = await client.PostAsync("/signup?email=ann@example.com", null);
Console.WriteLine($"/signup: {(int)signup.StatusCode}");

// The server's record: mail delivered to ann, waited for at most ten seconds, two in the one case that has none.
var ann = new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "ann@example.com" };
try
{
    await surl.WaitForEventAsync(ann, TimeSpan.FromSeconds(which == "no-plaintext" ? 2 : 10), CancellationToken.None);
    SurlMessagePage inbox = await surl.Mail.ListMessagesAsync("ann@example.com", "INBOX");
    SurlMailMessage mail = await surl.Mail.ReadMessageAsync("ann@example.com", "INBOX", inbox.Messages[0].Uid);
    string subject = System.Text.Encoding.ASCII.GetString(mail.Bytes.Span).Split("\r\n").First(line => line.StartsWith("Subject: "));
    Console.WriteLine($"mail to ann: {inbox.Messages.Count} message, {subject}");
    return 0;
}
catch (TimeoutException)
{
    Console.WriteLine("mail to ann: none");
    return 1;
}

// The app under test, started beside the server. Its configuration is whatever the test hands it.
sealed class App : IAsyncDisposable
{
    private readonly WebApplication web;

    private App(WebApplication web, Uri address)
    {
        this.web = web;
        Address = address;
    }

    public Uri Address { get; }

    public static async Task<App> StartAsync(IDictionary<string, string?> settings)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        // The default is a place that answers no one, so an app with nothing injected fails where a test can see it.
        builder.Configuration["Rates:Url"] = "http://127.0.0.1:1/";
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddHttpClient();
        WebApplication web = builder.Build();

        // GET /rates: the app asks its upstream for rates.json and passes the answer on.
        web.MapGet("/rates", async (IHttpClientFactory http, IConfiguration config) =>
        {
            try
            {
                string body = await http.CreateClient().GetStringAsync(config["Rates:Url"] + "rates.json");
                return Results.Text(body, "application/json");
            }
            catch (HttpRequestException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        });

        // POST /signup: the app mails a welcome to the address it was given.
        web.MapPost("/signup", async (string email, IConfiguration config) =>
        {
            try
            {
                using var smtp = new SmtpClient(config["Smtp:Host"], int.Parse(config["Smtp:Port"] ?? "25"))
                {
                    Credentials = new NetworkCredential("app@example.com", "apppass"),
                };
                await smtp.SendMailAsync("app@example.com", email, "Welcome", "Thanks for signing up.");
                return Results.Accepted();
            }
            catch (SmtpException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        });

        await web.StartAsync();
        string bound = web.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new App(web, new Uri(bound));
    }

    public async ValueTask DisposeAsync()
    {
        await web.StopAsync();
        await web.DisposeAsync();
    }
}
