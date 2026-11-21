// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Rotates a shared password on an in-process server that serves HTTPS and FTPS: adds the new
// account beside the old, moves each client, reads which logins still name the old account,
// and removes it last.
// Run: dotnet run rotate.cs [-- rotate|in-place|last|open-session]
//   rotate        the whole rotation (the default)
//   in-place      Accounts.Add for a name that exists, then Remove and Add
//   last          Accounts.Remove of the only account, then a GET with no login
//   open-session  an FTP connection logged in as app-v1 before the removal, used after it
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "rotate";

// One server, two listeners, one account; its own throwaway certificate covers both.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0", "ftps://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("app-v1", "old-secret") },
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Info,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
string web = server.BoundUrls[0] + "hello.txt";
string ftp = server.BoundUrls[1] + "hello.txt";
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};

switch (mode)
{
    case "in-place":
        // The account book holds one password per name: Add does not change a password.
        try
        {
            server.Accounts.Add("app-v1", "new-secret");
        }
        catch (ArgumentException refused)
        {
            Console.WriteLine($"Add app-v1 again: {refused.GetType().Name}");
        }

        Console.WriteLine($"Remove app-v1: {server.Accounts.Remove("app-v1")}");
        server.Accounts.Add("app-v1", "new-secret");
        Console.WriteLine($"https app-v1 old password: {await Web("app-v1", "old-secret")}");
        Console.WriteLine($"https app-v1 new password: {await Web("app-v1", "new-secret")}");
        return 0;

    case "last":
        // Removing the only account does not lock everyone out: it leaves no login to ask for.
        Console.WriteLine($"Remove app-v1: {server.Accounts.Remove("app-v1")}");
        Console.WriteLine($"HasAccounts: {server.Accounts.HasAccounts}");
        Console.WriteLine($"https, no login: {await Web(null, null)}");
        Console.WriteLine(log.ToString().Split('\n').Single(l => l.Contains("no account left")).TrimEnd());
        return 0;

    case "open-session":
        // One FTP client logs in as app-v1 and keeps its connection; the account goes; it checks again.
        server.Accounts.Add("app-v2", "new-secret");
        await using (CurlClient job = CurlClient.Create())
        {
            Console.WriteLine($"ftps app-v1, first check: {await Ftp("app-v1", "old-secret", job)}");
            Console.WriteLine($"Remove app-v1: {server.Accounts.Remove("app-v1")}");
            Console.WriteLine($"ftps app-v1, same connection: {await Ftp("app-v1", "old-secret", job)}");
        }

        Console.WriteLine($"ftps app-v1, new connection: {await Ftp("app-v1", "old-secret")}");
        return 0;
}

// 1. The new password goes on a second account, beside the old one.
server.Accounts.Add("app-v2", "new-secret");
Console.WriteLine("added app-v2");

// 2. The web client moves; the FTP job has not yet. Both log in.
Console.WriteLine($"https app-v2: {await Web("app-v2", "new-secret")}");
Console.WriteLine($"ftps app-v1: {await Ftp("app-v1", "old-secret")}");

// 3. Who still uses the old password: the logins the server judged for app-v1.
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged, Account = "app-v1" }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"still on app-v1: {login.Scheme} {login.Mechanism} {(login.IsAccepted ? "accepted" : "refused")}");
}

// 4. The FTP job moves; then the old account goes.
Console.WriteLine($"ftps app-v2: {await Ftp("app-v2", "new-secret")}");
Console.WriteLine($"removed app-v1: {server.Accounts.Remove("app-v1")}");

// 5. The old password is refused everywhere; the new one is not.
Console.WriteLine($"https app-v1: {await Web("app-v1", "old-secret")}");
Console.WriteLine($"ftps app-v1: {await Ftp("app-v1", "old-secret")}");
Console.WriteLine($"https app-v2: {await Web("app-v2", "new-secret")}");
Console.WriteLine($"ftps app-v2: {await Ftp("app-v2", "new-secret")}");
return 0;

// The web client: a GET over HTTPS with Basic, trusting the server's certificate; the status code.
async Task<string> Web(string? user, string? password)
{
    var request = new CurlRequest(HttpMethod.Get, web) { Tls = trust };
    if (user is not null)
    {
        request = request with
        {
            Authentication = new CurlAuthenticationOptions { UserName = user, Password = password, HttpAuthentication = HttpAuthenticationMethods.Basic },
        };
    }

    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode.ToString();
}

// The FTP job: over FTPS, TLS from the first byte, it checks the file is there before its run.
// A HEAD logs in and asks SIZE, as curl -I does, with no data connection: the size, or the failure.
async Task<string> Ftp(string user, string password, CurlClient? shared = null)
{
    var request = new CurlRequest(HttpMethod.Head, ftp)
    {
        Tls = trust,
        Authentication = new CurlAuthenticationOptions { UserName = user, Password = password },
    };
    CurlClient client = shared ?? CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        return $"{response.Headers.Single(h => h.Key == "Content-Length").Value} bytes";
    }
    catch (CurlException failed)
    {
        return $"{failed.Code} ({(int)failed.Code})";
    }
    finally
    {
        if (shared is null)
        {
            await client.DisposeAsync();
        }
    }
}
