// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Test mailboxes that start with the folders a client expects: Surl.Api creates them at start from
// folders=, and the test lists them over IMAP with Curl.Api and from the store.
// Run: dotnet run start-folders.cs [-- <case>]
//   (no argument)  ann with Sent, Drafts and Archive; carol with none; dave and erin with odd names; a reset
//   twice          a folders= list that names Sent twice
//   empty          folders= with nothing after it
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "folders";

// IMAP on a free port, example.com; ann's mailbox starts with three folders, carol's with none,
// dave's with a nested name and one with a space, erin's with a comma written %2C.
var options = new SurlServerOptions { ListenUrls = { "imap://127.0.0.1:0" } };
options.MailDomains.Add("example.com");
options.Mailboxes.Add(mode switch
{
    "twice" => "ann@example.com:annpass;folders=Sent,Sent",
    "empty" => "ann@example.com:annpass;folders=",
    _ => "ann@example.com:annpass;folders=Sent,Drafts,Archive",
});
options.Mailboxes.Add("carol@example.com:carolpass");
options.Mailboxes.Add("dave@example.com:davepass;folders=Sent Items,Archive/2026");
options.Mailboxes.Add("erin@example.com:erinpass;folders=Paid%2CUnpaid");

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException refused)
{
    Console.WriteLine($"refused at start: {refused.ExitCode}, {refused.Lines[0]}");
    return 1;
}

await using (server)
{
    string imap = $"{server.BoundUrls[0]}";
    await using CurlClient client = CurlClient.Create();

    // One IMAP request through Curl.Api; the body is the answer, one line per entry.
    async Task<string[]> Imap(string user, string password, string path)
    {
        var request = new CurlRequest(HttpMethod.Get, imap + path) { Authentication = new CurlAuthenticationOptions { UserName = user, Password = password } };
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        return (await new StreamReader(body, Encoding.ASCII).ReadToEndAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    // The client's view: an IMAP URL with no folder lists them, one "* LIST" line each, name last.
    async Task<string> OverImap(string user, string password) =>
        string.Join(", ", (await Imap(user, password, "")).Select(line => line.Split(" \"/\" ")[1]));

    // The server's view, with no client: each folder and how many messages it holds.
    async Task<string> InStore(string owner) =>
        string.Join(", ", (await server.Mail.ListMailboxesAsync(owner)).Select(m => $"{m.Name} ({m.MessageCount})"));

    Console.WriteLine($"ann over IMAP: {await OverImap("ann@example.com", "annpass")}");
    Console.WriteLine($"ann in the store: {await InStore("ann@example.com")}");
    Console.WriteLine($"carol over IMAP: {await OverImap("carol@example.com", "carolpass")}");

    // A start folder is a real folder: it can be selected and searched, and holds nothing.
    Console.WriteLine($"ann SEARCH ALL in Sent: {string.Join(" ", await Imap("ann@example.com", "annpass", "Sent?ALL"))}");

    // Names: a space is kept and quoted, a slash nests, and the parent is implied; %2C is a comma.
    foreach (string line in await Imap("dave@example.com", "davepass", ""))
    {
        Console.WriteLine($"dave: {line}");
    }

    Console.WriteLine($"erin in the store: {await InStore("erin@example.com")}");

    // A reset empties every mailbox between tests and keeps the folders.
    await server.Mail.DeliverAsync("ann@example.com", "Subject: Hello\r\n\r\nHello.\r\n"u8.ToArray());
    Console.WriteLine($"after a delivery: {await InStore("ann@example.com")}");
    await server.ResetAsync();
    Console.WriteLine($"after ResetAsync: {await InStore("ann@example.com")}");
}

return 0;
