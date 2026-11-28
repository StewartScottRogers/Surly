// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Adds, replaces and removes the fixed Bearer token of an in-process HTTPS server, the account
// whose user name is empty, and checks after each step which tokens it accepts.
// Run: dotnet run rotate-token.cs [-- rotate|refusals]
//   rotate    add, replace and remove the token (the default)
//   refusals  a second Add, then SetPassword and Remove with no token set
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Output;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "rotate";

// An HTTPS server with no account, its own throwaway certificate, and a record of what it judged.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    SelfSigned = true,
    RecordExchanges = true,
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Info,
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
string url = server.BoundUrls[0] + "hello.txt";
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};

if (mode == "refusals")
{
    server.Accounts.Add("", "token-one");
    Console.WriteLine($"Add again: {Refusal(() => server.Accounts.Add("", "token-two"))}");
    Console.WriteLine($"token-one: {await Get("token-one")}");
    server.Accounts.Remove("");
    Console.WriteLine($"Remove, none set: {server.Accounts.Remove("")}");
    Console.WriteLine($"SetPassword, none set: {Refusal(() => server.Accounts.SetPassword("", "token-two"))}");
    return 0;
}

// 1. With no account the server needs no login. Adding the token closes it.
Console.WriteLine($"no token, no account: {await Get(null)}");
server.Accounts.Add("", "token-one");
Console.WriteLine($"no token: {await Get(null)}");
Console.WriteLine($"token-one: {await Get("token-one")}");

// 2. Replace it: the old token is refused from the next request on.
server.Accounts.SetPassword("", "token-two");
Console.WriteLine($"token-one: {await Get("token-one")}");
Console.WriteLine($"token-two: {await Get("token-two")}");

// 3. What the server judged: every token login is the account "bearer token", never the token.
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"judged: {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

// 4. Remove it: the old token is refused, and the server, with no account left, still asks for a login.
Console.WriteLine($"Remove: {server.Accounts.Remove("")}");
Console.WriteLine($"HasAccounts: {server.Accounts.HasAccounts}");
Console.WriteLine($"token-two: {await Get("token-two")}");
Console.WriteLine($"no token: {await Get(null)}");
Console.WriteLine(log.ToString().Split('\n').Single(l => l.Contains("account left")).TrimEnd());
Console.WriteLine($"tokens in the log: {log.ToString().Contains("token-one") || log.ToString().Contains("token-two")}");
return 0;

// A GET over HTTPS, with the token as a Bearer header or with none; the status code.
async Task<int> Get(string? token)
{
    var request = new CurlRequest(HttpMethod.Get, url) { Tls = trust };
    if (token is not null)
    {
        request = request with
        {
            Authentication = new CurlAuthenticationOptions { XOAuth2Bearer = token, HttpAuthentication = HttpAuthenticationMethods.Bearer },
        };
    }

    await using CurlClient client = CurlClient.Create();
    CurlResponse response = await client.SendAsync(request, CancellationToken.None);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null);
    return response.StatusCode;
}

// The type of the exception a call throws, or "no exception".
static string Refusal(Action call)
{
    try
    {
        call();
        return "no exception";
    }
    catch (ArgumentException refused)
    {
        return refused.GetType().Name;
    }
}
