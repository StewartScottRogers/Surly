// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Asks an XMPP server what it offers with service discovery, using Surl.Api and Curl.Api, in one process.
// Run: dotnet run program.cs
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Net.Sockets;
using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

// The server, in-process, with TLS: a certificate made by `openssl` in setup, and two accounts.
await using SurlServer server = await SurlServer.StartAsync(new SurlServerOptions
{
    ListenUrls = { "xmpp://127.0.0.1:0" },
    CertificateFile = "server.pem",
    KeyFile = "server.key",
    Accounts = { new SurlAccount("alice", "s3cret"), new SurlAccount("bob", "hunter2") },
});

// The client trusts that certificate, as curl --cacert does.
var tls = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem" };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = tls } });

await Ask("server", "127.0.0.1", "alice", "s3cret");
await Ask("account", "alice@127.0.0.1", "alice", "s3cret");
await Ask("?disco", "127.0.0.1?disco", "alice", "s3cret");
await Ask("?disco;items", "127.0.0.1?disco;items", "alice", "s3cret");
await Ask("node x", "127.0.0.1?disco;node=x", "alice", "s3cret");
await Ask("bob", "bob@127.0.0.1", "alice", "s3cret");
await Ask("example.com", "example.com", "alice", "s3cret");
await Ask("wrong password", "127.0.0.1", "alice", "wrong");
await Ask("no login", "127.0.0.1", null, null);

// curl asks for info only. To read the items answer, a program writes the stanzas itself, on a server
// that takes a password with no TLS, which is for a test.
await using SurlServer plain = await SurlServer.StartAsync(new SurlServerOptions
{
    ListenUrls = { "xmpp://127.0.0.1:0" },
    Accounts = { new SurlAccount("alice", "s3cret") },
    AllowPlaintextAuthentication = true,
});
await Raw(plain);
return 0;

// One question: the entity to ask is the path, and the answer prints on one line.
async Task Ask(string label, string path, string? user, string? password)
{
    var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}")
    {
        Authentication = new CurlAuthenticationOptions { UserName = user, Password = password },
    };
    try
    {
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using Stream body = response.Body;
        Console.WriteLine($"{label}: {(await new StreamReader(body).ReadToEndAsync()).Trim()}");
    }
    catch (CurlException failure)
    {
        Console.WriteLine($"{label}: CurlException {(int)failure.Code} {failure.Code}: {failure.Message}");
    }
}

// A client by hand: SASL PLAIN, a resource, then two IQs to the server and to the account.
static async Task Raw(SurlServer on)
{
    Uri address = on.BoundUrl;
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(address.Host, address.Port);
    NetworkStream stream = tcp.GetStream();
    const string Open = "<?xml version='1.0'?><stream:stream to='127.0.0.1' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' version='1.0'>";

    await Send(stream, Open);
    string plain = Convert.ToBase64String("\0alice\0s3cret"u8.ToArray());
    await Send(stream, $"<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='PLAIN'>{plain}</auth>");
    await Send(stream, Open);
    await Send(stream, "<iq type='set' id='b'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><resource>r</resource></bind></iq>");
    foreach ((string label, string to, string ns) in new[]
    {
        ("raw info, server", "127.0.0.1", "info"),
        ("raw items, server", "127.0.0.1", "items"),
        ("raw items, account", "alice@127.0.0.1", "items"),
    })
    {
        string answer = await Send(stream, $"<iq type='get' id='q' to='{to}'><query xmlns='http://jabber.org/protocol/disco#{ns}'/></iq>");
        Console.WriteLine($"{label}: {answer}");
    }
}

// Writes the bytes and reads what comes back until the server pauses.
static async Task<string> Send(NetworkStream stream, string xml)
{
    await stream.WriteAsync(Encoding.UTF8.GetBytes(xml));
    var received = new StringBuilder();
    var buffer = new byte[4096];
    using var pause = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
    try
    {
        while (true)
        {
            int n = await stream.ReadAsync(buffer, pause.Token);
            if (n == 0) break;
            received.Append(Encoding.UTF8.GetString(buffer, 0, n));
            pause.CancelAfter(TimeSpan.FromMilliseconds(500));
        }
    }
    catch (OperationCanceledException) { }
    return received.ToString();
}
