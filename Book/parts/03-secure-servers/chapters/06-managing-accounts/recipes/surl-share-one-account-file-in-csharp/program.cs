// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// One account file for four protocols: Surl.Api serves HTTPS, FTPS, IMAPS and MQTTS from users.txt,
// and Curl.Api logs in on each. Run: dotnet run program.cs -- alice s3cret [users-file]
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

string user = args[0];
string password = args[1];
string file = args.Length > 2 ? args[2] : "users.txt";

// One server, four listeners, one account file. An account given in code adds to the file's.
var options = new SurlServerOptions
{
    ListenUrls = { "https://127.0.0.1:0", "ftps://127.0.0.1:0", "imaps://127.0.0.1:0", "mqtts://127.0.0.1:0" },
    UserFile = file,
    Accounts = { new SurlAccount("carol", "c4rol") },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Topics = { "t/#;retain" },
};
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();

SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    // A file that cannot be used stops the start, before anything listens.
    Console.WriteLine($"start refused: {e.Message}");
    return 1;
}

await using (server)
{
    // The client: the same login on every listener, and the certificate to trust (curl -u, --cacert).
    var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" };
    await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });
    var login = new CurlAuthenticationOptions { UserName = user, Password = password };

    string https = server.BoundUrls[0].ToString();
    string ftps = server.BoundUrls[1].ToString();
    string imaps = server.BoundUrls[2].ToString();
    string mqtts = server.BoundUrls[3].ToString();

    Console.WriteLine($"https: {await Try(async () =>
    {
        CurlResponse r = await Send(new CurlRequest(HttpMethod.Get, https + "hello.txt") { Authentication = login });
        return $"{r.StatusCode} {(await Read(r)).Trim()}".Trim();
    })}");
    Console.WriteLine($"ftps: {await Try(async () => (await Read(await Send(new CurlRequest(HttpMethod.Get, ftps + "hello.txt") { Authentication = login }))).Trim())}");
    Console.WriteLine($"imaps: {await Try(async () => (await Read(await Send(new CurlRequest(HttpMethod.Get, imaps) { Authentication = login }))).Contains("INBOX") ? "INBOX listed" : "no INBOX")}");
    string published = await Try(async () =>
    {
        await Send(new CurlRequest(HttpMethod.Post, mqtts + "t/lamp") { Body = CurlContent.FromBytes("on"u8.ToArray()), Authentication = login });
        return "published";
    });
    Console.WriteLine($"mqtts: {published}");

    // The broker keeps what the login let through. A QoS 0 publish is not acknowledged, so the
    // client returns before the broker has stored it: wait for it, a moment at most.
    SurlRetainedMessage? kept = null;
    for (int tries = 0; tries < 50 && kept is null && published == "published"; tries++)
    {
        kept = await server.Messages.ReadRetainedAsync("t/lamp", CancellationToken.None);
        if (kept is null) await Task.Delay(100);
    }

    Console.WriteLine($"retained: {(kept is null ? "nothing" : Encoding.UTF8.GetString(kept.Payload.Span))}");

    Task<CurlResponse> Send(CurlRequest request) => client.SendAsync(request, CancellationToken.None);
}

return 0;

static async Task<string> Read(CurlResponse response)
{
    await using Stream stream = response.Body;
    return await new StreamReader(stream).ReadToEndAsync();
}

// The answer, or the CurlException's code: a refused FTPS, IMAPS or MQTTS login throws.
static async Task<string> Try(Func<Task<string>> call)
{
    try
    {
        return await call();
    }
    catch (CurlException failure)
    {
        return $"CurlException {(int)failure.Code} {failure.Code}";
    }
}
