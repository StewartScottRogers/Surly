// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Seeds one Redis key name into two databases through Surl.Api and reads each back through Surl.Api and Curl.Api.
// Run: dotnet run program.cs [-- case]
//   (no argument)    greeting in databases 1 and 2, read from both, and from database 0
//   delete           DEL in one database leaves the other
//   list             the keys of each database, and DBSIZE
//   range            database 16
//   no-login         a client with no login
//   no-plaintext     log in without AllowPlaintextAuthentication
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "apart";

// The server: a Redis store on a free port and one account, whose password may cross the plain listener.
var options = new SurlServerOptions
{
    ListenUrls = { "redis://127.0.0.1:0" },
    AllowPlaintextAuthentication = which != "no-plaintext",
};
options.Accounts.Add(new SurlAccount("alice", "s3cret"));
await using var server = await SurlServer.StartAsync(options);

// The client, shared by every read: it logs in as alice unless the case says otherwise.
await using CurlClient client = CurlClient.Create();

if (which is "no-login" or "no-plaintext")
{
    return await Refused(which == "no-login");
}

if (which == "range")
{
    return await Range();
}

// The seed: the same key name in two databases, each with its own value, with no client and no login.
await server.Keys.SetAsync("greeting", "from-app-one", null, 1);
await server.Keys.SetAsync("greeting", "from-app-two", null, 2);

switch (which)
{
    case "delete":
        // DEL runs in database 1 only, and answers how many keys it removed.
        Console.WriteLine($"curl DEL in database 1: {await Curl("1/DEL/greeting")}");
        await Show(1);
        await Show(2);
        break;
    case "list":
        await server.Keys.SetAsync("other", "x", null, 1);
        foreach (int database in new[] { 0, 1, 2 })
        {
            SurlKeyPage page = await server.Keys.ListAsync("*", null, 100, database);
            Console.WriteLine($"database {database}: [{string.Join(", ", page.Keys.Select(key => key.Key))}], DBSIZE {await Curl($"{database}/DBSIZE")}");
        }

        break;
    default:
        await Show(1);
        await Show(2);
        await Show(0);
        break;
}

return 0;

// One database as the test sees it: read without a client, then through the client over the URL.
async Task Show(int database)
{
    string seen;
    try
    {
        seen = $"\"{(await server.Keys.GetAsync("greeting", database)).Text}\"";
    }
    catch (SurlNotFoundException)
    {
        seen = "no such key";
    }

    // The database number is the first segment of the URL path, then the command and its arguments.
    string over = await Curl($"{database}/GET/greeting");
    Console.WriteLine($"database {database}: server {seen}, curl {(over == "" ? "null" : $"\"{over}\"")}");
}

// Redis over the URL path, as alice; the body is the answer plus a line feed, and null is a lone line feed.
async Task<string> Curl(string command, bool login = true)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{command}")
    {
        Authentication = login ? new CurlAuthenticationOptions { UserName = "alice", Password = "s3cret" } : null,
    };
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    return (await new StreamReader(body).ReadToEndAsync()).TrimEnd('\n');
}

// Only databases 0 to 15 exist, and each of the three ways in says so its own way.
async Task<int> Range()
{
    try
    {
        await server.Keys.SetAsync("greeting", "x", null, 16);
    }
    catch (ArgumentOutOfRangeException)
    {
        Console.WriteLine("Keys.SetAsync in database 16: refused");
    }

    try
    {
        await server.Keys.GetAsync("greeting", 16);
    }
    catch (SurlReadRefusedException)
    {
        Console.WriteLine("Keys.GetAsync in database 16: refused");
    }

    try
    {
        await Curl("16/GET/greeting");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"curl in database 16: error {(int)failure.Code}, message names {(failure.Message.Contains("DB index is out of range") ? "DB index is out of range" : failure.Message)}");
        return 0;
    }

    return 1;
}

// A login the server refuses is a CurlException, whose Code is the exit code curl would give.
async Task<int> Refused(bool noLogin)
{
    try
    {
        await Curl("1/GET/greeting", login: !noLogin);
        return 0;
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
        return (int)failure.Code;
    }
}
