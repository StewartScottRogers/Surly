// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Stands up a test environment in-process, HTTP, FTP, SMTP and IMAP on one server, and
// smoke-tests every listener before the suite would run: one request each, failing fast.
// Run: dotnet run smoke.cs [-- all|hung|gone]
//   all   every listener of the environment (the default)
//   hung  HTTP, then a listener that accepts but never answers, then IMAP
//   gone  a URL whose server has stopped, then FTP
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Surl.Api;

string mode = args.Length > 0 ? args[0] : "all";

// The environment: four listeners on free ports over one store, recording every exchange.
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0", "ftp://127.0.0.1:0", "smtp://127.0.0.1:0", "imap://127.0.0.1:0" },
    MailDomains = { "example.com" },
    Mailboxes = { "app@example.com:secret" },
    RecordExchanges = true,
};
options.Files["health.txt"] = "ok\n"u8.ToArray();
await using SurlServer environment = await SurlServer.StartAsync(options);
Uri http = environment.BoundUrls[0], ftp = environment.BoundUrls[1], imap = environment.BoundUrls[3];

int result;
switch (mode)
{
    case "hung":
        // An SMTP listener that holds every connection for a minute before it says anything.
        var hungOptions = new SurlServerOptions { ListenUrls = { "smtp://127.0.0.1:0" } };
        hungOptions.Faults.Add(SurlFault.Delay(TimeSpan.FromMinutes(1)));
        await using (SurlServer hung = await SurlServer.StartAsync(hungOptions))
        {
            result = await Smoke(http, hung.BoundUrl, imap);
        }

        break;

    case "gone":
        // A server that was started and stopped: nothing listens at its URL any more.
        SurlServer stopped = await SurlServer.StartAsync(new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } });
        Uri gone = stopped.BoundUrl;
        await stopped.DisposeAsync();
        result = await Smoke(gone, ftp);
        break;

    default:
        result = await Smoke([.. environment.BoundUrls]);

        // What the server saw: one exchange per listener.
        Console.WriteLine("exchanges: " + string.Join(", ", environment.Exchanges.GroupBy(x => x.Scheme).Select(g => $"{g.Key} {g.Count()}")));
        break;
}

return result;

// One quick request to each URL in turn, in its own protocol: a GET over HTTP, a NOOP over the
// others. A listener passes if it answers at all, a refusal included. The first that does not
// answer within 3 seconds ends the check with curl's exit code; the rest are not tried.
static async Task<int> Smoke(params Uri[] urls)
{
    foreach (Uri url in urls)
    {
        HttpMethod ask = url.Scheme == "http" ? HttpMethod.Get : new HttpMethod("NOOP");
        try
        {
            await using CurlClient client = CurlClient.Create(new CurlClientOptions { Timeout = TimeSpan.FromSeconds(3) });
            CurlResponse response = await client.SendAsync(new CurlRequest(ask, url.ToString()), CancellationToken.None);
            await response.Body.DisposeAsync();
            Console.WriteLine($"ok {url.Scheme}: {response.StatusCode}");
        }
        catch (CurlException failure) when (failure.Code is not (CurlCode.CouldntConnect or CurlCode.OperationTimedOut))
        {
            Console.WriteLine($"ok {url.Scheme}: refused, {failure.Code} ({(int)failure.Code})");
        }
        catch (CurlException failure)
        {
            Console.WriteLine($"FAIL {url}: {failure.Code} ({(int)failure.Code})");
            return (int)failure.Code;
        }
    }

    return 0;
}
