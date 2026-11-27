// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds an in-process Surl.Api server once, takes a snapshot, and restores it before every test, so
// each test starts from the seeded data whatever ran before it. A small runner stands in for the
// test framework.
// Run: dotnet run restore-before-each.cs [-- <case>]
//   (no argument)  seed, snapshot, then three tests in order, each after a restore
//   reverse        the same tests in the opposite order
//   none           nothing between tests
//   reset          ResetAsync before each test instead of RestoreAsync
//   strict         without AllowPlaintextAuthentication
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "in-order";

// One server: HTTP and FTP over one content store, SMTP into one mail store.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "ftp://127.0.0.1:0", "smtp://127.0.0.1:0" },
    AllowUploads = true,
    AllowPlaintextAuthentication = mode != "strict",
};
options.Accounts.Add(new SurlAccount("app", "apppass"));
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
await using SurlServer server = await SurlServer.StartAsync(options);
string http = $"{server.BoundUrls[0]}", ftp = $"{server.BoundUrls[1]}", smtp = $"{server.BoundUrls[2]}";

// Once, before the first test: seed a catalog of 100 items and ann's three earlier mails, then snapshot.
for (int i = 1; i <= 100; i++)
{
    await server.Files.PutAsync($"/catalog/item-{i:000}.json", Encoding.UTF8.GetBytes($"{{\"id\":{i},\"price\":10}}\n"));
}

for (int i = 1; i <= 3; i++)
{
    await server.Mail.DeliverAsync("ann@example.com", Encoding.UTF8.GetBytes($"From: shop@example.com\r\nSubject: Order {i}\r\n\r\nThanks.\r\n"));
}

SurlSnapshot seeded = await server.SnapshotAsync();
Console.WriteLine($"seeded and snapshot taken: {seeded.ByteCount} bytes");

// The tests: each changes the server through a client, then checks the catalog and ann's inbox.
await using CurlClient client = CurlClient.Create();
var login = new CurlAuthenticationOptions { UserName = "app", Password = "apppass" };
(string Name, Func<Task> Run)[] tests =
[
    ("Price_change_rewrites_one_item", async () =>
    {
        await Send(new CurlRequest(HttpMethod.Put, $"{http}catalog/item-007.json")
        {
            Authentication = login with { HttpAuthentication = HttpAuthenticationMethods.Digest },
            Body = "{\"id\":7,\"price\":12}\n"u8.ToArray(),
        });
        await Expect(item7: "{\"id\":7,\"price\":12}", mail: 3);
    }),
    ("Order_mail_adds_one_message", async () =>
    {
        await Send(new CurlRequest(HttpMethod.Put, $"{smtp}example.com")
        {
            Authentication = login,
            Mail = new CurlMailOptions { MailFrom = "shop@example.com", MailRecipients = ["ann@example.com"] },
            Body = CurlContent.FromStream(new MemoryStream("From: shop@example.com\r\nSubject: Order 4\r\n\r\nThanks.\r\n"u8.ToArray())),
        });
        await Expect(item7: "{\"id\":7,\"price\":10}", mail: 4);
    }),
    ("Ftp_reader_sees_the_seeded_price", async () =>
    {
        string item = await Send(new CurlRequest(HttpMethod.Get, $"{ftp}catalog/item-007.json") { Authentication = login });
        if (item.Trim() != "{\"id\":7,\"price\":10}")
        {
            throw new Exception($"FTP read {item.Trim()}");
        }

        await Expect(item7: "{\"id\":7,\"price\":10}", mail: 3);
    }),
];
if (mode == "reverse")
{
    Array.Reverse(tests);
}

int failed = 0;
foreach ((string name, Func<Task> run) in tests)
{
    if (mode == "reset")
    {
        await server.ResetAsync();
    }
    else if (mode != "none")
    {
        await server.RestoreAsync(seeded);
    }

    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception failure)
    {
        Console.WriteLine($"FAIL {name}: {(failure is CurlException curl ? $"CurlException {curl.Code}: " : "")}{failure.Message}");
        failed++;
    }
}

return failed == 0 ? 0 : 1;

async Task<string> Send(CurlRequest request)
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    return await new StreamReader(body).ReadToEndAsync();
}

// The whole catalog must still be there, item 7 as given, and ann's inbox must hold this many mails.
async Task Expect(string item7, int mail)
{
    int items = 0;
    string? after = null;
    SurlFolderPage page;
    do
    {
        page = await server.Files.ListAsync("/catalog", after, 1000);
        items += page.Entries.Count;
        after = page.Entries.Count > 0 ? page.Entries[^1].Name : after;
    }
    while (page.HasMore);

    string got7 = items == 0 ? "(none)" : Encoding.UTF8.GetString((await server.Files.ReadAsync("/catalog/item-007.json")).Bytes.Span).Trim();
    int gotMail = (await server.Mail.ListMessagesAsync("ann@example.com", "INBOX")).Messages.Count;
    if (items != 100 || got7 != item7 || gotMail != mail)
    {
        throw new Exception($"{items} items, item 7 {got7}, {gotMail} mails; want 100, {item7}, {mail}");
    }
}
