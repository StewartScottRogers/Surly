// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// An integration test's whole environment in one in-process Surl.Api server: HTTP, SMTP and IMAP,
// MQTT and Redis. The app talks to all of them with Curl.Api; the test seeds one store and checks
// the others, with no server process started.
// Run: dotnet run whole-environment.cs [-- <case>]
//   (no argument)  the app reads a flag from Redis, then ships order 7
//   flag-off       the test seeds the flag off: the app ships nothing
//   no-extensions  AreExtensionsTurnedOn = false
//   strict         without AllowPlaintextAuthentication
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "right";

// One server, five listeners on free ports; an account for the app and a mailbox for its customer.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "smtp://127.0.0.1:0", "imap://127.0.0.1:0", "mqtt://127.0.0.1:0", "redis://127.0.0.1:0" },
    AllowUploads = true,
    AllowPlaintextAuthentication = mode != "strict",
    AreExtensionsTurnedOn = mode != "no-extensions",
};
options.Accounts.Add(new SurlAccount("app", "apppass"));
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
options.Topics.Add("orders/#;retain");
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"start refused: {refused.ExitCode}: {refused.Message}");
    return 1;
}

await using (server)
{
    Uri http = server.BoundUrls[0], smtp = server.BoundUrls[1], imap = server.BoundUrls[2], mqtt = server.BoundUrls[3], redis = server.BoundUrls[4];
    Console.WriteLine($"listening: {string.Join(" ", server.BoundUrls.Select(u => u.Scheme))}");

    // The test seeds the app's feature flag straight into the Redis keyspace.
    await server.Keys.SetAsync("feature:shipping", mode == "flag-off" ? "off" : "on");

    // The app ships order 7 if the flag says so: a Redis read, an upload, a mail, an event, a Redis write.
    var login = new CurlAuthenticationOptions { UserName = "app", Password = "apppass" };
    bool shipped = false;
    await using (CurlClient app = CurlClient.Create())
    {
        try
        {
            string flag = (await Text(app, new CurlRequest(HttpMethod.Get, $"{redis}GET/feature:shipping") { Authentication = login })).Trim();
            Console.WriteLine($"app read feature:shipping = {flag}");
            if (flag == "on")
            {
                await Text(app, new CurlRequest(HttpMethod.Put, $"{http}invoice-7.txt")
                {
                    Authentication = login with { HttpAuthentication = HttpAuthenticationMethods.Digest },
                    Body = "Invoice 7: 3 widgets, 42.00\n"u8.ToArray(),
                });
                await Text(app, new CurlRequest(HttpMethod.Put, $"{smtp}example.com")
                {
                    Authentication = login,
                    Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["ann@example.com"] },
                    Body = CurlContent.FromStream(new MemoryStream("From: app@example.com\r\nTo: ann@example.com\r\nSubject: Order 7 shipped\r\n\r\nOrder 7 is on its way.\r\n"u8.ToArray())),
                });
                await Text(app, new CurlRequest(HttpMethod.Post, $"{mqtt}orders/7") { Authentication = login, Body = "shipped"u8.ToArray() });
                await Text(app, new CurlRequest(HttpMethod.Get, $"{redis}SET/order:7/shipped") { Authentication = login });
                Console.WriteLine("app shipped order 7");
                shipped = true;
            }
        }
        catch (CurlException failed)
        {
            Console.WriteLine($"app failed: CurlException {failed.Code}: {failed.Message}");
            return 1;
        }
    }

    // The test: ann reads her mail over IMAP as a client would; the rest straight from the stores.
    string inbox;
    await using (CurlClient ann = CurlClient.Create())
    {
        inbox = await Text(ann, new CurlRequest(HttpMethod.Get, $"{imap}INBOX?SUBJECT%20%22Order%207%22") { Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = "annpass" } });
    }

    // The MQTT publish is QoS 0, never acknowledged, so the broker may keep it a moment after the app
    // is done: the test reads again until it is there, for at most 10 s.
    SurlRetainedMessage? retained = await server.Messages.ReadRetainedAsync("orders/7");
    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
    while (shipped && retained is null && DateTime.UtcNow < deadline)
    {
        await Task.Delay(100);
        retained = await server.Messages.ReadRetainedAsync("orders/7");
    }

    var checks = new (string What, string Got, string Want)[]
    {
        ("IMAP search for ann's order mail", inbox.Trim(), "* SEARCH 1"),
        ("file invoice-7.txt", await FileText("/invoice-7.txt"), "Invoice 7: 3 widgets, 42.00"),
        ("retained on orders/7", retained is null ? "(none)" : Encoding.UTF8.GetString(retained.Payload.Span), "shipped"),
        ("redis order:7", await KeyText("order:7"), "shipped"),
    };
    foreach ((string what, string got, string want) in checks)
    {
        Console.WriteLine(got == want ? $"{what}: {got}" : $"FAIL {what}: {got}, want {want}");
    }

    return checks.All(c => c.Got == c.Want) ? 0 : 1;
}

// The body of one request as text.
static async Task<string> Text(CurlClient client, CurlRequest request)
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body).ReadToEndAsync();
}

async Task<string> FileText(string path)
{
    try
    {
        return Encoding.UTF8.GetString((await server.Files.ReadAsync(path)).Bytes.Span).TrimEnd();
    }
    catch (SurlNotFoundException)
    {
        return "(none)";
    }
}

async Task<string> KeyText(string key)
{
    try
    {
        return (await server.Keys.GetAsync(key)).Text ?? "";
    }
    catch (SurlNotFoundException)
    {
        return "(none)";
    }
}
