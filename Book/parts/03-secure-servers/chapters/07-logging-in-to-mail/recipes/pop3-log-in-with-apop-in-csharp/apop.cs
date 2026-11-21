// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A POP3 client that logs in with APOP: an in-process Surl server that either names APOP or does not, and
// Curl.Api clients that log in to it, with what the server judged and what crossed the wire read back.
// Run: dotnet run apop.cs [-- <case>]
//   (no argument)  a server that names APOP with AuthenticationMethods: login, digest, wrong password, USER and PASS
//   default        the default server: no timestamp, APOP refused, Curl.Api logs in with CRAM-MD5
//   both           APOP and CRAM-MD5 named together
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "apop";

// POP3 for example.com with one mailbox. APOP is not in the default methods: it is named, alone or beside CRAM-MD5.
var options = new SurlServerOptions { ListenUrls = { "pop3://127.0.0.1:0" }, RecordExchanges = true };
options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
if (mode != "default")
{
    options.AuthenticationMethods = mode == "both" ? ["apop", "cram-md5"] : ["apop"];
}

await using var server = await SurlServer.StartAsync(options);
Uri pop3 = server.BoundUrl;

// Raw POP3: the commands written by hand, every reply read back as lines.
async Task<string[]> Talk(string commands)
{
    using var tcp = new TcpClient(pop3.Host, pop3.Port);
    using NetworkStream wire = tcp.GetStream();
    await wire.WriteAsync(Encoding.ASCII.GetBytes(commands + "QUIT\r\n"));
    return (await new StreamReader(wire, Encoding.ASCII).ReadToEndAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}

// One login on a client of its own, since a client reuses a connection that is already logged in.
async Task<string> Login(string password)
{
    await using CurlClient client = CurlClient.Create();
    try
    {
        CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, pop3.ToString())
        {
            Authentication = new CurlAuthenticationOptions { UserName = "ann@example.com", Password = password },
        }, CancellationToken.None);
        await response.Body.DisposeAsync();
        return "ok";
    }
    catch (CurlException failed)
    {
        return $"curl error {(int)failed.Code}";
    }
}

// The greeting, and a raw APOP with a made-up digest.
string[] raw = await Talk("APOP ann@example.com 0123456789abcdef0123456789abcdef\r\n");
Console.WriteLine($"greeting: {(Regex.IsMatch(raw[0], @"^\+OK surl ready <[0-9a-f]+\.\d+@surl>$") ? "ends in a timestamp" : raw[0])}");
Console.WriteLine($"raw APOP: {raw[1]}");

Console.WriteLine($"Curl.Api login: {await Login("annpass")}");

// What the server judged, and what the client sent.
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"login judged: {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

if (mode == "apop")
{
    // The exchange of that login: the server's timestamp is in what it sent, the APOP line in what the client sent.
    SurlExchangeRecord exchange = server.FindExchanges(new SurlEventQuery { Scheme = "pop3" }).Last(x => Encoding.ASCII.GetString(x.Received).Contains("APOP ann@"));
    string sent = Encoding.ASCII.GetString(exchange.Sent), received = Encoding.ASCII.GetString(exchange.Received);
    string stamp = Regex.Match(sent, @"<[^>]+>").Value;
    string digest = Regex.Match(received, @"APOP ann@example\.com ([0-9a-f]{32})").Groups[1].Value;
    string expected = Convert.ToHexStringLower(MD5.HashData(Encoding.ASCII.GetBytes(stamp + "annpass")));
    Console.WriteLine($"digest is MD5 of timestamp and password: {digest == expected}");
    Console.WriteLine($"password on the wire: {received.Contains("annpass")}");
    Console.WriteLine($"wrong password: {await Login("wrong")}");

    string[] userPass = await Talk("USER ann@example.com\r\nPASS annpass\r\n");
    Console.WriteLine($"USER: {userPass[1]}");
    Console.WriteLine($"CAPA lists SASL: {(await Talk("CAPA\r\n")).Any(l => l.StartsWith("SASL"))}");
}

return 0;
