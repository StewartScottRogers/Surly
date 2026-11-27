// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds a message into a mailbox with Surl.Api, then reads it back over IMAP with Curl.Api.
// Run: dotnet run seed-and-read.cs [-- bob|wrong-password]
//   (no argument)   seed a message for alice, fetch it over IMAP, show its flags before and after
//   bob             seed a message for an account the server does not have
//   wrong-password  seed as usual, then fetch with the wrong password
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "read";

// The server: IMAP on a free port of the loopback address, and one account.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.Accounts.Add(new SurlAccount("alice", "secret"));
await using var server = await SurlServer.StartAsync(options);

// The seed: one message, delivered the way mail that arrived over SMTP is delivered.
string to = mode == "bob" ? "bob@example.com" : "alice@example.com";
byte[] message = "From: shop@example.com\r\nTo: alice@example.com\r\nSubject: Your order\r\n\r\nIt has shipped.\r\n"u8.ToArray();
try
{
    await server.Mail.DeliverAsync(to, message);
}
catch (SurlSeedRefusedException refused)
{
    Console.WriteLine($"seed refused: {refused.Store} {refused.Refusal}");
    return 1;
}

await PrintMessages("seeded");

// The client: fetch UID 1 from alice's INBOX, logged in as alice.
CurlRequest fetch = new(HttpMethod.Get, $"{server.BoundUrl}INBOX;UID=1")
{
    Authentication = new CurlAuthenticationOptions
    {
        UserName = "alice",
        Password = mode == "wrong-password" ? "wrong" : "secret",
    },
};
await using (CurlClient client = CurlClient.Create())
{
    try
    {
        CurlResponse response = await client.SendAsync(fetch, CancellationToken.None);
        await using Stream body = response.Body;
        Console.Write(await new StreamReader(body).ReadToEndAsync());
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
        return (int)failure.Code;
    }
}

await PrintMessages("fetched");
return 0;

// The store as the test sees it, read without a client: UID, size and flags of each message.
async Task PrintMessages(string when)
{
    SurlMessagePage page = await server.Mail.ListMessagesAsync("alice", "INBOX");
    foreach (SurlMessageSummary each in page.Messages)
    {
        Console.WriteLine($"{when}: UID {each.Uid}, {each.Size} bytes, flags [{string.Join(" ", each.Flags)}]");
    }
}
