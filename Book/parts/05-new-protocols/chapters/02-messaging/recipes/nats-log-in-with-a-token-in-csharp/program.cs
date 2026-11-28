// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Logs in to an in-process NATS server with a token, with Surl.Api for the server and Curl.Api for the client, and reads
// the server's log for what it did with the token. Run: dotnet run program.cs [-- case]
//   (no argument)  a server that takes anonymous clients accepts a token unchecked
//   clear          a token over nats://, no loosening: refused
//   clear-log      the log's reason for that
//   loose          AllowPlaintextAuthentication, a wrong token: refused
//   loose-log      the log's reason for that
//   loose-right    AllowPlaintextAuthentication, the token the server holds: accepted
//   tls-wrong      natss://, a wrong token
//   tls-none       natss://, no token
//   untrusted      natss://, no trusted certificate
//   wrong-scheme   nats:// to a natss server
//   masked         the natss server's log shows the token masked
//   not-in-log     and nowhere else
//   right          natss://, the token the server holds
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.7

using System.Text.RegularExpressions;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Tls;
using Surl.Api;
using Surl.Output;

string which = args.Length > 0 ? args[0] : "solution";
const string Token = "tok123";
bool tls = which is "tls-wrong" or "tls-none" or "untrusted" or "wrong-scheme" or "masked" or "not-in-log" or "right";

// The server: the token is an account with no name. Its verbose log is kept, for the claims about what it did.
var log = new StringWriter();
var options = new SurlServerOptions
{
    ListenUrls = { tls ? "natss://127.0.0.1:0" : "nats://127.0.0.1:0" },
    Log = TextWriter.Synchronized(log),
    LogLevel = LogLevel.Verbose,
};
if (which == "solution")
{
    options.AllowAnonymous = true;
}
else
{
    options.Accounts.Add(new SurlAccount("", Token));
}

options.AllowPlaintextAuthentication = which is "loose" or "loose-log" or "loose-right";
if (tls)
{
    options.CertificateFile = "server.pem";
    options.KeyFile = "server.key";
}

await using var server = await SurlServer.StartAsync(options);
string url = server.BoundUrls[0].ToString();

// The client. --ssl-no-revoke is SslOptions.NoRevoke: the Windows build needs it with this certificate.
var trusted = new CurlTlsPeerOptions { CertificateAuthorityFile = "server.pem", SslOptions = SslOptions.NoRevoke };
await using CurlClient client = CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = trusted } });
await using CurlClient bare = CurlClient.Create();

switch (which)
{
    case "clear":
    case "clear-log":
        await Publish(bare, url, Token);
        break;
    case "loose":
    case "loose-log":
        await Publish(bare, url, "wrong");
        break;
    case "loose-right":
        await Publish(bare, url, Token);
        break;
    case "tls-wrong":
        await Publish(client, url, "wrong");
        break;
    case "tls-none":
        await Publish(client, url, null);
        break;
    case "untrusted":
        await Publish(bare, url, Token);
        break;
    case "wrong-scheme":
        await Publish(client, url.Replace("natss://", "nats://"), Token);
        break;
    case "masked":
    case "not-in-log":
    case "right":
        await Publish(client, url, which is "right" or "not-in-log" ? Token : "wrong");
        break;
    default:
        await Publish(bare, url, Token);
        break;
}

// The log is read once the server has stopped, when every line of every exchange is in it.
if (which is "clear-log" or "loose-log" or "masked" or "not-in-log")
{
    await server.DisposeAsync();
    string[] lines = log.ToString().Split('\n');
    switch (which)
    {
        case "clear-log":
            Console.WriteLine(Reason(lines));
            break;
        case "loose-log":
            Console.WriteLine(Reason(lines));
            break;
        case "masked":
            Console.WriteLine(Regex.Match(lines.First(line => line.Contains("auth_token")), "\"auth_token\":\"[^\"]*\"").Value);
            break;
        default:
            Console.WriteLine($"lines with the token: {lines.Count(line => line.Contains(Token))}");
            break;
    }
}

return 0;

// A login is a POST of a message to a subject; the token is the user name, with no password. A refusal is a CurlException.
// A TLS error's wording is the TLS library's, so only its code is shown.
static async Task Publish(CurlClient client, string url, string? token)
{
    var request = new CurlRequest(HttpMethod.Post, url + "orders.new")
    {
        Authentication = token is null ? null : new CurlAuthenticationOptions { UserName = token },
        Body = CurlContent.FromBytes("paid"u8.ToArray()),
    };
    try
    {
        await client.SendAsync(request, CancellationToken.None);
        Console.WriteLine("published");
    }
    catch (CurlException failure)
    {
        Console.WriteLine((int)failure.Code == 60
            ? $"refused: curl error {(int)failure.Code}"
            : $"refused: curl error {(int)failure.Code}: {failure.Message}");
    }
}

// The reason the server gives for a refused login, from its log: the word in brackets after "was refused".
static string Reason(string[] lines)
{
    string line = lines.First(l => l.Contains("login was refused"));
    int open = line.IndexOf('(');
    return line[(open + 1)..line.IndexOf(')', open)];
}
