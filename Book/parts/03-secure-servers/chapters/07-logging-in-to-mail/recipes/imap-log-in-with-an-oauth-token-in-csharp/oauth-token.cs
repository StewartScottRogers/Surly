// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A mail client that logs in to IMAP with an OAuth 2.0 token: an in-process Surl server that holds the token
// as an account, and Curl.Api clients that present it over TLS and over plain IMAP.
// Run: dotnet run oauth-token.cs [-- <case>]
//   (no argument)  the token over imaps and over plain imap, then what the server advertised and judged
//   loose          the same token over plain imap on a server with AllowPlaintextAuthentication
//   one            a server that offers OAUTHBEARER alone, and a client that insists on XOAUTH2
#:package Surl.Api@1.0.0
#:package Curl.Api@0.2.0

using System.Net.Sockets;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;
using Surl.Protocol.Abstractions;

string mode = args.Length > 0 ? args[0] : "default";

// imaps (TLS, a self-signed certificate) and imap on one mail store. The token is an account with an
// empty user name; ann and bob are the mailboxes.
var options = new SurlServerOptions
{
    ListenUrls = { "imaps://127.0.0.1:0", "imap://127.0.0.1:0" },
    SelfSigned = true,
    AllowPlaintextAuthentication = mode == "loose",
    RecordExchanges = true,
};
options.Accounts.Add(new SurlAccount("", "tok-abc123"));
if (mode == "one")
{
    options.AuthenticationMethods = ["oauthbearer"];
}

options.MailDomains.Add("example.com");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("bob@example.com:bobpass");
await using var server = await SurlServer.StartAsync(options);
await server.Mail.DeliverAsync("ann@example.com", "From: ann@example.com\r\nTo: ann@example.com\r\nSubject: Lunch\r\n\r\nNoon at the usual place.\r\n"u8.ToArray());
string imaps = $"{server.BoundUrls[0]}", imap = $"{server.BoundUrls[1]}";

var trustIt = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityBlob = Encoding.ASCII.GetBytes(server.SelfSignedCertificatePem!) } };

// One request on a client of its own, since a client reuses a connection that is already logged in and would
// skip the login: the body, or the curl error. user is the name the client gives; token what it sends.
async Task<string> Fetch(string label, string url, string user, string token, string? mechanism = null)
{
    await using CurlClient client = CurlClient.Create();
    var request = new CurlRequest(HttpMethod.Get, url)
    {
        Authentication = new CurlAuthenticationOptions { UserName = user, XOAuth2Bearer = token, LoginOptions = mechanism },
        Tls = trustIt,
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        return $"{label}: ok: {(await new StreamReader(body, Encoding.ASCII).ReadToEndAsync()).Trim()}";
    }
    catch (CurlException failed)
    {
        return $"{label}: curl error {(int)failed.Code}: {failed.Message}";
    }
}

const string Message = "INBOX;UID=1;SECTION=TEXT";

// Raw IMAP: AUTHENTICATE XOAUTH2 written by hand, whatever the server advertises; prints the tagged reply.
async Task<string> Raw(string label, string url, string token)
{
    var uri = new Uri(url);
    using var tcp = new TcpClient(uri.Host, uri.Port);
    using NetworkStream wire = tcp.GetStream();
    string payload = Convert.ToBase64String(Encoding.ASCII.GetBytes($"user=ann@example.com\u0001auth=Bearer {token}\u0001\u0001"));
    await wire.WriteAsync(Encoding.ASCII.GetBytes($"a1 AUTHENTICATE XOAUTH2 {payload}\r\na2 LOGOUT\r\n"));
    string replies = await new StreamReader(wire, Encoding.ASCII).ReadToEndAsync();
    return $"{label}: {replies.Split("\r\n").First(line => line.StartsWith("a1 "))}";
}

// The mechanisms a listener advertised, read from the first line the server sent in the first exchange it recorded.
string Advertised(string scheme)
{
    SurlExchangeRecord first = server.FindExchanges(new SurlEventQuery { Scheme = scheme }).First();
    string greeting = Encoding.ASCII.GetString(first.Sent).Split("\r\n")[0];
    return string.Join(" ", greeting.Split(' ', ']').Where(word => word.StartsWith("AUTH=")));
}

switch (mode)
{
    case "loose":
        Console.WriteLine(await Fetch("imap, token", imap + Message, "ann@example.com", "tok-abc123"));
        Console.WriteLine(await Fetch("imap, wrong token", imap + Message, "ann@example.com", "wrong-token"));
        Console.WriteLine(await Raw("imap, raw XOAUTH2, right token", imap, "tok-abc123"));
        break;
    case "one":
        Console.WriteLine(await Fetch("imaps, default mechanism", imaps + Message, "ann@example.com", "tok-abc123"));
        Console.WriteLine(await Fetch("imaps, XOAUTH2 insisted on", imaps + Message, "ann@example.com", "tok-abc123", "AUTH=XOAUTH2"));
        Console.WriteLine($"imaps advertises: {Advertised("imaps")}");
        break;
    default:
        Console.WriteLine(await Fetch("imaps, token", imaps + Message, "ann@example.com", "tok-abc123"));
        Console.WriteLine(await Fetch("imaps, XOAUTH2", imaps + Message, "ann@example.com", "tok-abc123", "AUTH=XOAUTH2"));
        Console.WriteLine(await Fetch("imaps, wrong token", imaps + Message, "ann@example.com", "wrong-token"));
        Console.WriteLine(await Fetch("imaps, bob with the token", imaps + Message, "bob@example.com", "tok-abc123"));
        Console.WriteLine(await Fetch("imap, token", imap + Message, "ann@example.com", "tok-abc123"));
        Console.WriteLine(await Raw("imap, raw XOAUTH2, right token", imap, "tok-abc123"));
        Console.WriteLine(await Raw("imap, raw XOAUTH2, wrong token", imap, "wrong-token"));
        Console.WriteLine($"imaps advertises: {Advertised("imaps")}");
        Console.WriteLine($"imap advertises: {Advertised("imap")}");
        break;
}

// What the server judged, in order, and on which scheme.
foreach (LoginJudgedEvent login in server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged }).Cast<LoginJudgedEvent>())
{
    Console.WriteLine($"login judged: {login.Scheme} {login.Mechanism} {login.Account} {(login.IsAccepted ? "accepted" : "refused")}");
}

return 0;
