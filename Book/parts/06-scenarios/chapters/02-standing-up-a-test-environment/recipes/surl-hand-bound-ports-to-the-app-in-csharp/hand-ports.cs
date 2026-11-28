// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Starts HTTP and SMTP on free ports with Surl.Api, hands the bound URLs to the app under test,
// and checks what the server recorded.
// Run: dotnet run hand-ports.cs [-- app|twice|fixed-port]
//   app         one server, handed to the app (the default)
//   twice       two servers at once, as two parallel test runs would start them
//   fixed-port  a second server asking for the port the first one bound
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "app";

await using SurlServer server = await StartTestServer("http://127.0.0.1:0", "smtp://127.0.0.1:0");

switch (mode)
{
    case "twice":
        await using (SurlServer other = await StartTestServer("http://127.0.0.1:0", "smtp://127.0.0.1:0"))
        {
            Console.WriteLine($"first:  {string.Join(" ", server.BoundUrls)}");
            Console.WriteLine($"second: {string.Join(" ", other.BoundUrls)}");
            Console.WriteLine(server.BoundUrls.Intersect(other.BoundUrls).Any() ? "a port is shared" : "no port is shared");
        }

        return 0;

    case "fixed-port":
        try
        {
            await StartTestServer(server.BoundUrls[0].ToString(), "smtp://127.0.0.1:0");
            return 0;
        }
        catch (SurlServerStartException refused)
        {
            Console.WriteLine($"refused: {refused.ExitCode} ({(int)refused.ExitCode})");
            Console.WriteLine(refused.Message);
            return (int)refused.ExitCode;
        }
}

// BoundUrls is in the order of ListenUrls, port 0 replaced by the port each listener got.
Uri httpUrl = server.BoundUrls[0];
Uri smtpUrl = server.BoundUrls[1];
Console.WriteLine($"the app gets {httpUrl} and {smtpUrl}");
await RunApp(httpUrl, smtpUrl);

// What the server recorded, read from its mail store without a client.
foreach (SurlMessageSummary message in (await server.Mail.ListMessagesAsync("app@example.com", "INBOX")).Messages)
{
    Console.WriteLine($"stored: UID {message.Uid}, {message.Size} bytes");
}

Console.WriteLine($"server stopped: {await server.StopAsync()}");
return 0;

// The test environment: a greeting over HTTP and one mailbox over SMTP, held in memory.
static async Task<SurlServer> StartTestServer(string httpListenUrl, string smtpListenUrl)
{
    var options = new SurlServerOptions { ListenUrls = { httpListenUrl, smtpListenUrl } };
    options.Files["greeting.txt"] = "Welcome aboard.\n"u8.ToArray();
    options.MailDomains.Add("example.com");
    options.Mailboxes.Add("app@example.com:secret");
    return await SurlServer.StartAsync(options);
}

// Stands in for the app under test: it knows its two servers only by the URLs it is handed.
static async Task RunApp(Uri httpUrl, Uri smtpUrl)
{
    await using CurlClient client = CurlClient.Create();
    CurlResponse page = await client.SendAsync(new CurlRequest(HttpMethod.Get, $"{httpUrl}greeting.txt"), CancellationToken.None);
    string greeting;
    await using (Stream body = page.Body)
    {
        greeting = (await new StreamReader(body).ReadToEndAsync()).TrimEnd();
    }

    var mail = new CurlRequest(HttpMethod.Put, $"{smtpUrl}example.com")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "app@example.com", Password = "secret" },
        Mail = new CurlMailOptions { MailFrom = "app@example.com", MailRecipients = ["app@example.com"] },
        Body = CurlContent.FromStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"Subject: Welcome\r\n\r\n{greeting}\r\n"))),
    };
    CurlResponse sent = await client.SendAsync(mail, CancellationToken.None);
    await sent.Body.DisposeAsync();
    Console.WriteLine($"the app mailed: {greeting} (SMTP {sent.StatusCode})");
}
