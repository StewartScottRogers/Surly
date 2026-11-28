// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Tests an app written against HttpClient: the app logs in with a password, gets a token and
// calls the API with it, through Curl.Api's HttpMessageHandler, against an in-process HTTPS server.
// Run: dotnet run app-test.cs [-- flow|wrong-password|untrusted|credentials]
//   flow            the app's whole flow, then what the server recorded (the default)
//   wrong-password  the app logs in with a wrong password
//   untrusted       the CurlClient does not trust the server's certificate
//   credentials     the handler's Credentials log in instead of the app's own header
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "flow";

// The server: a password account, a Bearer token (the account with no user name), the token
// endpoint and the API, over HTTPS with a throwaway certificate.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0" },
    SelfSigned = true,
    Accounts = { new SurlAccount("app", "s3cret"), new SurlAccount("", "tok-7f3a9c") },
    RecordExchanges = true,
};
options.Files["token.txt"] = "tok-7f3a9c\n"u8.ToArray();
options.Files["api/orders.json"] = "[{\"id\":1,\"total\":40}]\n"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);

// The app's HttpClient, built on a CurlClient that trusts exactly the server's certificate.
var trust = new CurlTlsOptions
{
    Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) },
};
await using CurlClient curl = CurlClient.Create(new CurlClientOptions { Tls = mode == "untrusted" ? null : trust });
var handler = new CurlHttpMessageHandler(curl);
using var http = new HttpClient(handler) { BaseAddress = server.BoundUrl };
var app = new OrdersApp(http);

try
{
    if (mode == "credentials")
    {
        // The handler logs in for the app, as SocketsHttpHandler would: the server's challenge first.
        handler.Credentials = new NetworkCredential("app", "s3cret");
        using HttpResponseMessage token = await http.GetAsync("token.txt");
        Console.WriteLine($"token.txt: {(int)token.StatusCode}");
    }
    else
    {
        string token = await app.LogInAsync("app", mode == "wrong-password" ? "guess" : "s3cret");
        Console.WriteLine($"token: {token}");
        Console.Write($"orders: {await app.GetOrdersAsync(token)}");
    }
}
catch (HttpRequestException failed)
{
    // A status the app refused carries StatusCode; a failed transfer carries the CurlException inside.
    string why = failed.StatusCode is { } status ? $"status {(int)status}"
        : failed.InnerException is CurlException curlFailed ? $"{curlFailed.Code} ({(int)curlFailed.Code})" : "?";
    Console.WriteLine($"{failed.GetType().Name}: {why}");
    return 1;
}

// What the server recorded: each login it judged, in order.
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"server: {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

return 0;

// The app under test, written against HttpClient alone: it knows nothing of Curl.Api or surl.
sealed class OrdersApp(HttpClient http)
{
    public async Task<string> LogInAsync(string user, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "token.txt");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        using HttpResponseMessage response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsStringAsync()).Trim();
    }

    public async Task<string> GetOrdersAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/orders.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
