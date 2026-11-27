// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Shows that an XMPP server requires TLS before a login, with Surl.Api for the server and Curl.Api and a plain socket for
// the clients. Run: dotnet run program.cs [-- case]
//   (no argument)  Curl.Api upgrades with STARTTLS, logs in and asks the server's disco info
//   offered        a raw client sends its stream header: what does the server offer?
//   early          a raw client sends a PLAIN login before STARTTLS
//   manual         a raw client upgrades with STARTTLS itself, then logs in
//   xmpps          Curl.Api over xmpps://
//   untrusted      Curl.Api without the certificate
//   wrong          a wrong password
//   no-login       no login
//   no-cert        a server with no certificate and no loosening
//   plain-offered  a server with AllowPlaintextAuthentication and no certificate: what does it offer?
//   plain-login    the same server accepts a PLAIN login in clear
//   plain-curl     the same server, Curl.Api with no TLS
//   plain-wrong    the same server, a wrong password
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Surl.Api;

string which = args.Length > 0 ? args[0] : "solution";
const string Header = "<?xml version='1.0'?><stream:stream to='127.0.0.1' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' version='1.0'>";
// SASL PLAIN is the account name and the password after a NUL byte each, in base64: here alice and s3cret.
string plainLogin = "<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='PLAIN'>" + Convert.ToBase64String("\0alice\0s3cret"u8.ToArray()) + "</auth>";

bool plain = which.StartsWith("plain-");
var options = new SurlServerOptions
{
    ListenUrls = { "xmpp://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
};
if (plain)
{
    options.AllowPlaintextAuthentication = true;
}
else if (which != "no-cert")
{
    options.ListenUrls.Add("xmpps://127.0.0.1:0");
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

if (which == "no-cert")
{
    try
    {
        await using var refused = await SurlServer.StartAsync(options);
        Console.WriteLine("started");
    }
    catch (SurlServerStartException failure)
    {
        Console.WriteLine($"SurlServerStartException: exit {(int)failure.ExitCode}, {failure.Message}");
    }

    return 0;
}

await using var server = await SurlServer.StartAsync(options);
Uri xmpp = server.BoundUrls[0];
var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" };
await using CurlClient trusting = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });
await using CurlClient bare = CurlClient.Create();

switch (which)
{
    case "offered":
    case "plain-offered":
    {
        using var raw = new RawClient(xmpp);
        await raw.SendAsync(Header);
        Console.WriteLine(Between(await raw.ReadUntilAsync("</stream:features>"), "<stream:features>", "</stream:features>"));
        break;
    }
    case "early":
    case "plain-login":
    {
        using var raw = new RawClient(xmpp);
        await raw.SendAsync(Header + plainLogin);
        string answer = await raw.ReadUntilAsync(which == "early" ? "</failure>" : "<success");
        Console.WriteLine(Regex.Match(answer, "<failure.*</failure>|<success[^>]*>").Value);
        break;
    }
    case "manual":
        await Manual(xmpp);
        break;
    case "xmpps":
        await Disco(trusting, server.BoundUrls[1], "alice", "s3cret");
        break;
    case "untrusted":
        await Disco(bare, xmpp, "alice", "s3cret");
        break;
    case "wrong":
        await Disco(trusting, xmpp, "alice", "wrong");
        break;
    case "no-login":
        await Disco(trusting, xmpp, null, null);
        break;
    case "plain-curl":
        await Disco(bare, xmpp, "alice", "s3cret");
        break;
    case "plain-wrong":
        await Disco(bare, xmpp, "alice", "wrong");
        break;
    default:
        await Disco(trusting, xmpp, "alice", "s3cret");
        break;
}

return 0;

static string Between(string text, string from, string to)
{
    int start = text.IndexOf(from, StringComparison.Ordinal);
    int end = text.IndexOf(to, StringComparison.Ordinal) + to.Length;
    return text[start..end];
}

// Curl.Api upgrades with STARTTLS itself when the server asks, logs in, and asks the server's domain what it is.
// A refusal is a CurlException; a TLS error's wording is the TLS library's, so only its code is shown.
static async Task Disco(CurlClient client, Uri server, string? user, string? password)
{
    var request = new CurlRequest(HttpMethod.Get, server + "127.0.0.1")
    {
        Authentication = user is null ? null : new CurlAuthenticationOptions { UserName = user, Password = password },
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        Console.Write(await new StreamReader(body).ReadToEndAsync());
    }
    catch (CurlException failure)
    {
        Console.WriteLine((int)failure.Code == 60
            ? $"refused: curl error {(int)failure.Code}"
            : $"refused: curl error {(int)failure.Code}: {failure.Message}");
    }
}

// What curl does for a client, by hand: ask for STARTTLS, run the handshake on the same connection, start the stream
// again, and only then log in.
static async Task Manual(Uri xmpp)
{
    using var raw = new RawClient(xmpp);
    await raw.SendAsync(Header);
    await raw.ReadUntilAsync("</stream:features>");
    await raw.SendAsync("<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>");
    Console.WriteLine(Regex.Match(await raw.ReadUntilAsync("<proceed"), "<proceed[^>]*>").Value);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var certificate = X509Certificate2.CreateFromPemFile("server.pem", "server.key");
    var secure = new SslStream(raw.Stream, false, (_, presented, _, _) => presented?.GetCertHashString() == certificate.GetCertHashString());
    await secure.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "127.0.0.1" }, cts.Token);
    raw.Stream = secure;

    await raw.SendAsync(Header);
    string features = await raw.ReadUntilAsync("</stream:features>");
    Console.WriteLine(features.Contains("<mechanism>PLAIN</mechanism>") ? "mechanisms offered, PLAIN among them" : "no PLAIN");
    await raw.SendAsync(plainLoginFor());
    Console.WriteLine(Regex.Match(await raw.ReadUntilAsync("<success"), "<success[^>]*>").Value);

    static string plainLoginFor() =>
        "<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='PLAIN'>" + Convert.ToBase64String("\0alice\0s3cret"u8.ToArray()) + "</auth>";
}

// A client that sends and reads XML as it is, with no library between: the bytes of a chat client's first minute.
sealed class RawClient : IDisposable
{
    private readonly TcpClient tcp = new();

    public RawClient(Uri server)
    {
        tcp.Connect(server.Host, server.Port);
        Stream = tcp.GetStream();
    }

    public Stream Stream { get; set; }

    public async Task SendAsync(string xml) => await Stream.WriteAsync(Encoding.UTF8.GetBytes(xml));

    // Reads until the text holds the marker, with a bound: a server that never sends it fails the read, not the run.
    public async Task<string> ReadUntilAsync(string marker)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var all = new StringBuilder();
        var buffer = new byte[4096];
        while (!all.ToString().Contains(marker))
        {
            int count = await Stream.ReadAsync(buffer, cts.Token);
            if (count == 0)
            {
                throw new EndOfStreamException($"the server closed the connection before {marker}");
            }

            all.Append(Encoding.UTF8.GetString(buffer, 0, count));
        }

        return all.ToString();
    }

    public void Dispose() => tcp.Dispose();
}
