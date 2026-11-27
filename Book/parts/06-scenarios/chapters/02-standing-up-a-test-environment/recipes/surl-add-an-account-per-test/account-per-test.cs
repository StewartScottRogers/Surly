// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Tests share one in-process Surl.Api server. Each test adds an account of its own before it runs
// and removes it, and the files it made, after; the account cannot outlive the test.
// Run: dotnet run account-per-test.cs [-- <case>]
//   (no argument)  two tests in turn, each with its own account, cleaned up after
//   overlap        two tests' accounts at once: what one can read of the other's files
//   imap           a test account over IMAP
//   leftover       a test that never removed its account, and the next run that adds it again
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "in-turn";

// One server for every test: HTTP and IMAP, uploads on, and a standing account of the fixture's own,
// so the server needs a login however many test accounts come and go.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "imap://127.0.0.1:0" },
    AllowUploads = true,
    RecordExchanges = true,
};
options.Accounts.Add(new SurlAccount("fixture", "fixture-secret"));
await using SurlServer server = await SurlServer.StartAsync(options);
string http = $"{server.BoundUrls[0]}", imap = $"{server.BoundUrls[1]}";
await using CurlClient client = CurlClient.Create();

switch (mode)
{
    case "overlap":
        server.Accounts.Add("test-1", "test-1-secret");
        server.Accounts.Add("test-2", "test-2-secret");
        await Send(Put("test-1", "test-1.txt"));
        Console.WriteLine($"test-2 reads test-1.txt: {await Send(Get("test-2", "test-1.txt"))}");
        return 1;

    case "imap":
        server.Accounts.Add("test-1", "test-1-secret");
        Console.WriteLine($"test-1 lists its IMAP mailboxes: {await Imap("test-1", "")}");
        Console.WriteLine($"test-1 reads its INBOX: {await Imap("test-1", "INBOX?ALL")}");
        server.Accounts.Remove("test-1");
        // The client's connection is still logged in as test-1 and keeps its login; a new one logs in afresh.
        Console.WriteLine($"test-1 on its open connection after Remove: {await Imap("test-1", "")}");
        await using (CurlClient fresh = CurlClient.Create())
        {
            Console.WriteLine($"test-1 logs in afresh after Remove: {await Imap("test-1", "", fresh)}");
        }

        return 1;

    case "leftover":
        server.Accounts.Add("test-1", "test-1-secret");
        // ... the test crashes here, and nothing removes the account ...
        try
        {
            server.Accounts.Add("test-1", "test-1-secret");
        }
        catch (Exception refused)
        {
            Console.WriteLine($"adding test-1 again: {refused.GetType().Name}: {refused.Message}");
        }

        return 1;
}

// Two tests in turn, each wrapped the same way: an account before, the account and its files gone after.
foreach (string test in new[] { "test-1", "test-2" })
{
    server.Accounts.Add(test, $"{test}-secret");
    try
    {
        Console.WriteLine($"{test}: upload {await Send(Put(test, $"{test}.txt"))}, read back {await Send(Get(test, $"{test}.txt"))}");
    }
    finally
    {
        await server.Files.DeleteAsync($"/{test}.txt");
        server.Accounts.Remove(test);
    }
}

Console.WriteLine($"test-1 logs in afterwards: {await Send(Get("test-1", "test-1.txt"))}");
Console.WriteLine($"files left: {(await server.Files.ListAsync("/")).Entries.Count}");
IEnumerable<StoreChangedEvent> changes = server.FindEvents(new SurlEventQuery { Store = StoreKind.Accounts }).Cast<StoreChangedEvent>();
Console.WriteLine($"accounts store: {string.Join(", ", changes.Select(c => $"{c.Change} {c.Name}"))}");
return 0;

// Requests as a test makes them, logged in as its own account with Digest.
CurlRequest Put(string user, string path) => new(HttpMethod.Put, $"{http}{path}") { Authentication = Login(user), Body = "test result\n"u8.ToArray() };

CurlRequest Get(string user, string path) => new(HttpMethod.Get, $"{http}{path}") { Authentication = Login(user) };

static CurlAuthenticationOptions Login(string user) =>
    new() { UserName = user, Password = $"{user}-secret", HttpAuthentication = HttpAuthenticationMethods.Digest };

async Task<string> Send(CurlRequest request)
{
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return $"{response.StatusCode}";
}

// An IMAP request: "ok", or the CurlException code it threw.
async Task<string> Imap(string user, string path, CurlClient? on = null)
{
    try
    {
        CurlResponse response = await (on ?? client).SendAsync(new CurlRequest(HttpMethod.Get, $"{imap}{path}") { Authentication = new CurlAuthenticationOptions { UserName = user, Password = $"{user}-secret" } }, CancellationToken.None);
        await response.Body.DisposeAsync();
        return "ok";
    }
    catch (CurlException failed)
    {
        return $"CurlException {failed.Code}: {failed.Message}";
    }
}
