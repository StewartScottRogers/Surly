// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A WebTransport server that needs a login, hosted with Surl.Api; Curl.Api opens a session,
// and the program reads what surl recorded about it.
// Run: dotnet run check-the-session-login.cs -- <case>
//   login       log in as player1 (the default)
//   none        no login            wrong     a wrong password
//   untrusted   log in, but do not trust the certificate
//   no-cert     host webtransport with no certificate
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@*

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "login";

// The server: WebTransport over QUIC on a free UDP port, one account, every exchange recorded.
var options = new SurlServerOptions { ListenUrls = { "webtransport://127.0.0.1:0" }, RecordExchanges = true };
if (which != "no-cert")
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

options.Accounts.Add(new SurlAccount("player1", "secret"));
SurlServer server;
try
{
    server = await SurlServer.StartAsync(options);
}
catch (SurlServerStartException e)
{
    Console.WriteLine($"start refused: {e.ExitCode} ({(int)e.ExitCode})");
    Console.WriteLine(e.Message);
    return (int)e.ExitCode;
}

int exit = 0;
await using (server)
{
    ReadOnlyMemory<byte>? authority = File.ReadAllBytes("server.pem");
    await using CurlClient client = CurlClient.Create(new CurlClientOptions
    {
        Tls = which == "untrusted" ? null : new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = authority } },
        Authentication = which switch
        {
            "none" => null,
            "wrong" => new CurlAuthenticationOptions { UserName = "player1", Password = "guess" },
            _ => new CurlAuthenticationOptions { UserName = "player1", Password = "secret" },
        },
    });

    // The session, with the game state as the request body, bounded at 10 seconds.
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
        CurlResponse response = await client.SendAsync(
            new CurlRequest(HttpMethod.Post, $"{server.BoundUrl}game?wt=datagram") { Body = """{"player":1,"x":3,"y":4}"""u8.ToArray() },
            timeout.Token);
        await using Stream body = response.Body;
        var echoed = new MemoryStream();
        await body.CopyToAsync(echoed, timeout.Token);
        Console.WriteLine($"session opened; {echoed.Length} bytes echoed");
    }
    catch (CurlException e)
    {
        Console.WriteLine($"refused: {e.Code} ({(int)e.Code}): {e.Message}");
        exit = (int)e.Code;
    }

    // What surl recorded: the CONNECT it received, its answer, and the login it judged.
    var logins = new SurlEventQuery { Kind = SurlEventKind.LoginJudged };
    if (which is "login" or "wrong")
    {
        await server.WaitForEventAsync(logins, TimeSpan.FromSeconds(10), CancellationToken.None);
    }

    SurlExchangeRecord? exchange = server.Exchanges.FirstOrDefault();
    foreach (SurlExchangeChunk chunk in exchange?.Chunks.Where(c => c.Layer == ExchangeBytesLayer.Decoded) ?? [])
    {
        string head = Encoding.ASCII.GetString(chunk.Bytes.Span);
        string[] wanted = [":method:", ":protocol:", ":path:", ":status:"];
        foreach (string line in head.Split("\r\n").Where(l => wanted.Any(l.StartsWith)))
        {
            Console.WriteLine($"{(chunk.Direction == SurlExchangeDirection.Received ? "received" : "sent")} {line}");
        }
    }

    foreach (LoginJudgedEvent login in server.FindEvents(logins).Cast<LoginJudgedEvent>())
    {
        Console.WriteLine($"login: {login.Account}, {login.Mechanism}, accepted {login.IsAccepted}");
    }
}

return exit;
