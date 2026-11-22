// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds an in-process server's mail and files, catches each seed the server refuses, reads why,
// and checks that a refused seed left nothing behind and that a client is refused the same way.
// Run: dotnet run catch-the-refusal.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

// SMTP and HTTP over one server: one mail domain, one mailbox, one account that may upload,
// and a 64-byte limit on any file or message.
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0", "http://127.0.0.1:0" },
    MailDomains = { "example.com" },
    Mailboxes = { "ann@example.com:secret" },
    Accounts = { new SurlAccount("tester", "secret") },
    AllowUploads = true,
    MaxFileSize = 64,
    RecordExchanges = true,
};
await using SurlServer server = await SurlServer.StartAsync(options);
byte[] mail = "Subject: Hello\r\n\r\nHi.\r\n"u8.ToArray();

// The seeds a test might write, each tried in turn.
await Seed("mail to ann@example.com", () => server.Mail.DeliverAsync("ann@example.com", mail));
await Seed("mail to bob@example.com", () => server.Mail.DeliverAsync("bob@example.com", mail));
await Seed("mail to not-an-address", () => server.Mail.DeliverAsync("not-an-address", mail));
await Seed("file /.surl/seed.txt", () => server.Files.PutAsync("/.surl/seed.txt", "x"u8.ToArray()));
await Seed("file /big.txt, 100 bytes", () => server.Files.PutAsync("/big.txt", new byte[100]));
await Seed("file seed.txt", () => server.Files.PutAsync("seed.txt", "x"u8.ToArray()));

// What the server recorded: only the seed it took changed a store.
foreach (StoreChangedEvent change in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.StoreChanged }).Cast<StoreChangedEvent>())
{
    Console.WriteLine($"recorded: {change.Store} {change.Change} {change.Name}");
}

// A client is refused the reserved path the same way: a PUT, logged in and with uploads on.
await using (CurlClient client = CurlClient.Create())
{
    var put = new CurlRequest(HttpMethod.Put, $"{server.BoundUrls[1]}.surl/seed.txt")
    {
        Body = "x"u8.ToArray(),
        Authentication = new CurlAuthenticationOptions { UserName = "tester", Password = "secret", HttpAuthentication = HttpAuthenticationMethods.Digest },
    };
    CurlResponse response = await client.SendAsync(put, CancellationToken.None);
    await response.Body.DisposeAsync();
    Console.WriteLine($"HTTP PUT /.surl/seed.txt: {response.StatusCode}");
}

return 0;

// One seed: done, refused by the store (which store, its name for the refusal, the message),
// or misused (a path no request may name).
static async Task Seed(string what, Func<Task> seed)
{
    try
    {
        await seed();
        Console.WriteLine($"{what}: seeded");
    }
    catch (SurlSeedRefusedException refused)
    {
        Console.WriteLine($"{what}: refused by {refused.Store}, {refused.Refusal}: {refused.Message}");
    }
    catch (ArgumentException misused)
    {
        Console.WriteLine($"{what}: ArgumentException: {misused.Message}");
    }
}
